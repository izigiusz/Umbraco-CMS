using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Exceptions;
using Umbraco.Cms.Core.Packaging;
using Umbraco.Cms.Core.Semver;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;
using Umbraco.Cms.Infrastructure.Persistence;

namespace Umbraco.Cms.Infrastructure.Runtime;

/// <summary>
/// Represents the state of the Umbraco runtime.
/// </summary>
public class RuntimeState : IRuntimeState
{
    internal const string PendingPackageMigrationsStateKey = "PendingPackageMigrations";

    private readonly IOptions<GlobalSettings> _globalSettings = null!;
    private readonly IOptions<UnattendedSettings> _unattendedSettings = null!;
    private readonly IUmbracoVersion _umbracoVersion = null!;
    private readonly IUmbracoDatabaseFactory _databaseFactory = null!;
    private readonly ILogger<RuntimeState> _logger = null!;
    private readonly PendingPackageMigrations _packageMigrationState = null!;
    private readonly Dictionary<string, object> _startupState = new Dictionary<string, object>();
    private readonly IConflictingRouteService _conflictingRouteService = null!;
    private readonly IEnumerable<IDatabaseProviderMetadata> _databaseProviderMetadata = null!;
    private readonly IRuntimeModeValidationService _runtimeModeValidationService = null!;

    /// <summary>
    /// The initial <see cref="RuntimeState"/>
    /// </summary>
    public static RuntimeState Booting() => new RuntimeState() { Level = RuntimeLevel.Boot };

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeState" /> class.
    /// </summary>
    private RuntimeState()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeState" /> class.
    /// </summary>
    public RuntimeState(
       IOptions<GlobalSettings> globalSettings,
       IOptions<UnattendedSettings> unattendedSettings,
       IUmbracoVersion umbracoVersion,
       IUmbracoDatabaseFactory databaseFactory,
       ILogger<RuntimeState> logger,
       PendingPackageMigrations packageMigrationState,
       IConflictingRouteService conflictingRouteService,
       IEnumerable<IDatabaseProviderMetadata> databaseProviderMetadata,
       IRuntimeModeValidationService runtimeModeValidationService)
    {
        _globalSettings = globalSettings;
        _unattendedSettings = unattendedSettings;
        _umbracoVersion = umbracoVersion;
        _databaseFactory = databaseFactory;
        _logger = logger;
        _packageMigrationState = packageMigrationState;
        _conflictingRouteService = conflictingRouteService;
        _databaseProviderMetadata = databaseProviderMetadata;
        _runtimeModeValidationService = runtimeModeValidationService;
    }

    /// <inheritdoc />
    public Version Version => _umbracoVersion.Version;

    /// <inheritdoc />
    public string VersionComment => _umbracoVersion.Comment;

    /// <inheritdoc />
    public SemVersion SemanticVersion => _umbracoVersion.SemanticVersion;

    /// <inheritdoc />
    public string? CurrentMigrationState { get; private set; }

    /// <inheritdoc />
    public string? FinalMigrationState { get; private set; }

    /// <inheritdoc />
    public RuntimeLevel Level { get; internal set; } = RuntimeLevel.Unknown;

    /// <inheritdoc />
    public RuntimeLevelReason Reason { get; internal set; } = RuntimeLevelReason.Unknown;

    /// <inheritdoc />
    public BootFailedException? BootFailedException { get; internal set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> StartupState => _startupState;

    /// <inheritdoc />
    public void DetermineRuntimeLevel()
    {
        if (_databaseFactory.Configured == false)
        {
            // local version *does* match code version, but the database is not configured
            // install - may happen with Deploy/Cloud/etc
            if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
            {
                _logger.LogDebug("Database is not configured, need to install Umbraco.");
            }

            Level = RuntimeLevel.Install;
            Reason = RuntimeLevelReason.InstallNoDatabase;

            return;
        }

        // Validate runtime mode
        if (_runtimeModeValidationService.Validate(out var validationErrorMessage) == false)
        {
            _logger.LogError(validationErrorMessage);

            Level = RuntimeLevel.BootFailed;
            Reason = RuntimeLevelReason.BootFailedOnException;
            BootFailedException = new BootFailedException(validationErrorMessage);

            return;
        }

        // Check if we have multiple controllers with the same name.
        if (_conflictingRouteService.HasConflictingRoutes(out string controllerName))
        {
            var message = $"Conflicting routes, you cannot have multiple controllers with the same name: {controllerName}";
            _logger.LogError(message);

            Level = RuntimeLevel.BootFailed;
            Reason = RuntimeLevelReason.BootFailedOnException;
            BootFailedException = new BootFailedException(message);

            return;
        }

        // Check the database state, whether we can connect or if it's in an upgrade or empty state, etc...
        switch (GetUmbracoDatabaseState(_databaseFactory))
        {
            case UmbracoDatabaseState.CannotConnect:
                {
                // cannot connect to configured database, this is bad, fail
                    if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
                    {
                        _logger.LogDebug("Could not connect to database.");
                    }

                    if (_databaseProviderMetadata.CanForceCreateDatabase(_databaseFactory))
                    {
                        // ok to install on a configured but missing database
                        Level = RuntimeLevel.BootFailed;
                        Reason = RuntimeLevelReason.InstallMissingDatabase;
                        return;
                    }

                    // else it is bad enough that we want to throw
                    Reason = RuntimeLevelReason.BootFailedCannotConnectToDatabase;
                    BootFailedException = new BootFailedException("A connection string is configured but Umbraco could not connect to the database.");
                    throw BootFailedException;
                }
            case UmbracoDatabaseState.NotInstalled:
                {
                    // ok to install on an empty database
                    Level = RuntimeLevel.Install;
                    Reason = RuntimeLevelReason.InstallEmptyDatabase;
                    return;
                }
            case UmbracoDatabaseState.NeedsUpgrade:
                {
                // the db version does not match... but we do have a migration table
                // so, at least one valid table, so we quite probably are installed & need to upgrade

                // although the files version matches the code version, the database version does not
                // which means the local files have been upgraded but not the database - need to upgrade
                    if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
                    {
                        _logger.LogDebug("Has not reached the final upgrade step, need to upgrade Umbraco.");
                    }
                    Level = _unattendedSettings.Value.UpgradeUnattended ? RuntimeLevel.Run : RuntimeLevel.Upgrade;
                    Reason = RuntimeLevelReason.UpgradeMigrations;
                }
                break;
            case UmbracoDatabaseState.NeedsPackageMigration:

                // no matter what the level is run for package migrations.
                // they either run unattended, or only manually via the back office.
                Level = RuntimeLevel.Run;

                if (_unattendedSettings.Value.PackageMigrationsUnattended)
                {
                    if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
                    {
                        _logger.LogDebug("Package migrations need to execute.");
                    }
                    Reason = RuntimeLevelReason.UpgradePackageMigrations;
                }
                else
                {
                    _logger.LogInformation("Package migrations need to execute but unattended package migrations is disabled. They will need to be run from the back office.");
                    Reason = RuntimeLevelReason.Run;
                }

                break;
            case UmbracoDatabaseState.Ok:
            default:
                {


                    // the database version matches the code & files version, all clear, can run
                    Level = RuntimeLevel.Run;
                    Reason = RuntimeLevelReason.Run;
                }
                break;
        }
    }

    public void Configure(RuntimeLevel level, RuntimeLevelReason reason, Exception? bootFailedException = null)
    {
        Level = level;
        Reason = reason;

        if (bootFailedException != null)
        {
            BootFailedException = new BootFailedException(bootFailedException.Message, bootFailedException);
        }
    }

    private enum UmbracoDatabaseState
    {
        Ok,
        CannotConnect,
        NotInstalled,
        NeedsUpgrade,
        NeedsPackageMigration
    }

    private UmbracoDatabaseState GetUmbracoDatabaseState(IUmbracoDatabaseFactory databaseFactory)
    {
        try
        {
            if (!TryDbConnect(databaseFactory))
            {
                return UmbracoDatabaseState.CannotConnect;
            }

            // no scope, no service - just directly accessing the database
            using (IUmbracoDatabase database = databaseFactory.CreateDatabase())
            {
                if (!database.IsUmbracoInstalled())
                {
                    return UmbracoDatabaseState.NotInstalled;
                }

                // Make ONE SQL call to determine Umbraco upgrade vs package migrations state.
                // All will be prefixed with the same key.
                IReadOnlyDictionary<string, string?>? keyValues = database.GetFromKeyValueTable(Constants.Conventions.Migrations.KeyValuePrefix);

                // This could need both an upgrade AND package migrations to execute, so always add any pending package migrations
                IReadOnlyList<string> packagesRequiringMigration = _packageMigrationState.GetPendingPackageMigrations(keyValues);
                _startupState[PendingPackageMigrationsStateKey] = packagesRequiringMigration;

                if (DoesUmbracoRequireUpgrade(keyValues))
                {
                    return UmbracoDatabaseState.NeedsUpgrade;
                }

                if (packagesRequiringMigration.Count > 0)
                {
                    return UmbracoDatabaseState.NeedsPackageMigration;
                }
            }

            return UmbracoDatabaseState.Ok;
        }
        catch (Exception e)
        {
            // can connect to the database so cannot check the upgrade state... oops
            _logger.LogWarning(e, "Could not check the upgrade state.");

            // else it is bad enough that we want to throw
            Reason = RuntimeLevelReason.BootFailedCannotCheckUpgradeState;
            BootFailedException = new BootFailedException("Could not check the upgrade state.", e);
            throw BootFailedException;
        }
    }

    private bool DoesUmbracoRequireUpgrade(IReadOnlyDictionary<string, string?>? keyValues)
    {
        var upgrader = new Upgrader(new UmbracoPlan(_umbracoVersion));
        var stateValueKey = upgrader.StateValueKey;

        if (keyValues?.TryGetValue(stateValueKey, out var value) ?? false)
        {
            CurrentMigrationState = value;
        }

        FinalMigrationState = upgrader.Plan.FinalState;
        if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
        {
            _logger.LogDebug("Final upgrade state is {FinalMigrationState}, database contains {DatabaseState}", FinalMigrationState, CurrentMigrationState ?? "<null>");
        }
        return CurrentMigrationState != FinalMigrationState;
    }

    private bool TryDbConnect(IUmbracoDatabaseFactory databaseFactory)
    {
        // anything other than install wants a database - see if we can connect
        // (since this is an already existing database, assume localdb is ready)
        bool canConnect;
        var tries = 5;
        for (var i = 0; ;)
        {
            canConnect = databaseFactory.CanConnect;
            if (canConnect || ++i == tries)
            {
                break;
            }
            if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
            {
                _logger.LogDebug("Could not immediately connect to database, trying again.");
            }
            Thread.Sleep(1000);
        }

        return canConnect;
    }
}
