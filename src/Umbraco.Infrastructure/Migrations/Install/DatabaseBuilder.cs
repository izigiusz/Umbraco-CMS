using System.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Install;
using Umbraco.Cms.Core.Install.Models;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Infrastructure.Migrations.Notifications;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.Dtos;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Umbraco.Cms.Infrastructure.Migrations.Install
{
    /// <summary>
    /// Supports building and configuring the database.
    /// </summary>
    public class DatabaseBuilder
    {
        private readonly IUmbracoDatabaseFactory _databaseFactory;
        private readonly ICoreScopeProvider _scopeProvider;
        private readonly IScopeAccessor _scopeAccessor;
        private readonly IRuntimeState _runtimeState;
        private readonly IKeyValueService _keyValueService;
        private readonly ILogger<DatabaseBuilder> _logger;
        private readonly IDbProviderFactoryCreator _dbProviderFactoryCreator;
        private readonly IConfigManipulator _configManipulator;
        private readonly IOptionsMonitor<GlobalSettings> _globalSettings;
        private readonly IOptionsMonitor<ConnectionStrings> _connectionStrings;
        private readonly IMigrationPlanExecutor _migrationPlanExecutor;
        private readonly DatabaseSchemaCreatorFactory _databaseSchemaCreatorFactory;
        private readonly IEnumerable<IDatabaseProviderMetadata> _databaseProviderMetadata;
        private readonly IEventAggregator _aggregator;

        private DatabaseSchemaResult? _databaseSchemaValidationResult;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseBuilder"/> class.
        /// </summary>
        public DatabaseBuilder(
            ICoreScopeProvider scopeProvider,
            IScopeAccessor scopeAccessor,
            IUmbracoDatabaseFactory databaseFactory,
            IRuntimeState runtimeState,
            ILoggerFactory loggerFactory,
            IKeyValueService keyValueService,
            IDbProviderFactoryCreator dbProviderFactoryCreator,
            IConfigManipulator configManipulator,
            IOptionsMonitor<GlobalSettings> globalSettings,
            IOptionsMonitor<ConnectionStrings> connectionStrings,
            IMigrationPlanExecutor migrationPlanExecutor,
            DatabaseSchemaCreatorFactory databaseSchemaCreatorFactory,
            IEnumerable<IDatabaseProviderMetadata> databaseProviderMetadata,
            IEventAggregator aggregator)
        {
            _scopeProvider = scopeProvider;
            _scopeAccessor = scopeAccessor;
            _databaseFactory = databaseFactory;
            _runtimeState = runtimeState;
            _logger = loggerFactory.CreateLogger<DatabaseBuilder>();
            _keyValueService = keyValueService;
            _dbProviderFactoryCreator = dbProviderFactoryCreator;
            _configManipulator = configManipulator;
            _globalSettings = globalSettings;
            _connectionStrings = connectionStrings;
            _migrationPlanExecutor = migrationPlanExecutor;
            _databaseSchemaCreatorFactory = databaseSchemaCreatorFactory;
            _databaseProviderMetadata = databaseProviderMetadata;
            _aggregator = aggregator;
        }

        #region Status

        /// <summary>
        /// Gets a value indicating whether the database is configured. It does not necessarily
        /// mean that it is possible to connect, nor that Umbraco is installed, nor up-to-date.
        /// </summary>
        public bool IsDatabaseConfigured => _databaseFactory.Configured;

        /// <summary>
        /// Gets a value indicating whether it is possible to connect to the configured database.
        /// It does not necessarily mean that Umbraco is installed, nor up-to-date.
        /// </summary>
        public bool CanConnectToDatabase => _databaseFactory.CanConnect;

        /// <summary>
        /// Verifies whether a it is possible to connect to a database.
        /// </summary>
        public bool CanConnect(string? connectionString, string providerName)
        {
            DbProviderFactory? factory = _dbProviderFactoryCreator.CreateFactory(providerName);
            return DbConnectionExtensions.IsConnectionAvailable(connectionString, factory);
        }

        public bool HasSomeNonDefaultUser()
        {
            using (ICoreScope scope = _scopeProvider.CreateCoreScope())
            {
                // look for the super user with default password
                NPoco.Sql<ISqlContext>? sql = _scopeAccessor.AmbientScope?.Database.SqlContext.Sql()
                    .SelectCount()
                    .From<UserDto>()
                    .Where<UserDto>(x => x.Id == Constants.Security.SuperUserId && x.Password == "default");
                var result = _scopeAccessor.AmbientScope?.Database.ExecuteScalar<int>(sql);
                var has = result != 1;
                if (has == false)
                {
                    // found only 1 user == the default user with default password
                    // however this always exists on uCloud, also need to check if there are other users too
                    result = _scopeAccessor.AmbientScope?.Database.ExecuteScalar<int>("SELECT COUNT(*) FROM umbracoUser");
                    has = result != 1;
                }
                scope.Complete();
                return has;
            }
        }

        internal bool IsUmbracoInstalled()
        {
            using (ICoreScope scope = _scopeProvider.CreateCoreScope(autoComplete: true))
            {
                return _scopeAccessor.AmbientScope?.Database.IsUmbracoInstalled() ?? false;
            }
        }

        #endregion

        #region Configure Connection String

        public bool ConfigureDatabaseConnection(DatabaseModel databaseSettings, bool isTrialRun)
        {
            IDatabaseProviderMetadata? providerMeta;

            // if the database model is null then we will attempt quick install.
            if (databaseSettings == null)
            {
                providerMeta = _databaseProviderMetadata.GetAvailable(true).FirstOrDefault();
                databaseSettings = new DatabaseModel
                {
                    DatabaseName = providerMeta?.DefaultDatabaseName!
                };
            }
            else
            {
                providerMeta = _databaseProviderMetadata.FirstOrDefault(x => x.Id == databaseSettings.DatabaseProviderMetadataId);
            }

            if (providerMeta == null)
            {
                throw new InstallException("Unable to determine database provider configuration.");
            }

            var connectionString = providerMeta.GenerateConnectionString(databaseSettings);
            var providerName = databaseSettings.ProviderName ?? providerMeta.ProviderName;

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(providerName) ||
                (providerMeta.RequiresConnectionTest && !CanConnect(connectionString, providerName)))
            {
                return false;
            }

            if (!isTrialRun)
            {
                // File configuration providers use a delay before reloading and triggering changes, so wait
                using var isChanged = new ManualResetEvent(false);
                using IDisposable? onChange = _connectionStrings.OnChange((options, name) =>
                {
                    // Only watch default named option (CurrentValue)
                    if (name != Options.DefaultName)
                    {
                        return;
                    }

                    // Signal change
                    isChanged.Set();
                });

                // Update configuration and wait for change
                _configManipulator.SaveConnectionStringAsync(connectionString, providerName).GetAwaiter().GetResult();
                if (!isChanged.WaitOne(10_000))
                {
                    throw new InstallException("Didn't retrieve updated connection string within 10 seconds, try manual configuration instead.");
                }

                Configure(providerMeta.ForceCreateDatabase);
            }

            return true;
        }

        public Task<Attempt<InstallOperationStatus>> ValidateDatabaseConnectionAsync(DatabaseModel databaseSettings)
        {
            IDatabaseProviderMetadata? providerMeta = _databaseProviderMetadata.FirstOrDefault(x => x.Id == databaseSettings.DatabaseProviderMetadataId);

            if (providerMeta is null)
            {
                return Task.FromResult(Attempt.Fail(InstallOperationStatus.UnknownDatabaseProvider));
            }

            var connectionString = providerMeta.GenerateConnectionString(databaseSettings);
            var providerName = databaseSettings.ProviderName ?? providerMeta.ProviderName;

            if (string.IsNullOrEmpty(connectionString))
            {
                return Task.FromResult(Attempt.Fail(InstallOperationStatus.MissingConnectionString));
            }

            if (string.IsNullOrEmpty(providerName))
            {
                return Task.FromResult(Attempt.Fail(InstallOperationStatus.MissingProviderName));
            }

            if (providerMeta.RequiresConnectionTest && CanConnect(connectionString, providerName) is false)
            {
                return Task.FromResult(Attempt.Fail(InstallOperationStatus.DatabaseConnectionFailed));
            }

            return Task.FromResult(Attempt.Succeed(InstallOperationStatus.Success));
        }

        private void Configure(bool installMissingDatabase)
        {
            _databaseFactory.Configure(_connectionStrings.CurrentValue);

            if (installMissingDatabase)
            {
                CreateDatabase();
            }
        }

        #endregion

        #region Database Schema

        public void CreateDatabase() => _dbProviderFactoryCreator.CreateDatabase(_databaseFactory.ProviderName!, _databaseFactory.ConnectionString!);

        /// <summary>
        /// Validates the database schema.
        /// </summary>
        /// <remarks>
        /// <para>This assumes that the database exists and the connection string is
        /// configured and it is possible to connect to the database.</para>
        /// </remarks>
        public DatabaseSchemaResult? ValidateSchema()
        {
            using (ICoreScope scope = _scopeProvider.CreateCoreScope())
            {
                DatabaseSchemaResult? result = ValidateSchema(scope);
                scope.Complete();
                return result;
            }
        }

        private DatabaseSchemaResult? ValidateSchema(ICoreScope scope)
        {
            if (_databaseFactory.Initialized == false)
            {
                return new DatabaseSchemaResult();
            }

            if (_databaseSchemaValidationResult != null)
            {
                return _databaseSchemaValidationResult;
            }

            _databaseSchemaValidationResult = _scopeAccessor.AmbientScope?.Database.ValidateSchema();

            scope.Complete();

            return _databaseSchemaValidationResult;
        }

        /// <summary>
        /// Creates the database schema and inserts initial data.
        /// </summary>
        /// <remarks>
        /// <para>This assumes that the database exists and the connection string is
        /// configured and it is possible to connect to the database.</para>
        /// </remarks>
        public Result? CreateSchemaAndData()
        {
            using (ICoreScope scope = _scopeProvider.CreateCoreScope())
            {
                Result? result = CreateSchemaAndData(scope);
                if (result?.Success is true)
                {
                    scope.Notifications.Publish(new DatabaseSchemaAndDataCreatedNotification(result!.RequiresUpgrade));
                }
                scope.Complete();
                return result;
            }
        }

        private Result? CreateSchemaAndData(ICoreScope scope)
        {
            try
            {
                Attempt<Result?> readyForInstall = CheckReadyForInstall();
                if (readyForInstall.Success == false)
                {
                    return readyForInstall.Result;
                }

                _logger.LogInformation("Database configuration status: Started");

                IUmbracoDatabase? database = _scopeAccessor.AmbientScope?.Database;

                var message = string.Empty;

                DatabaseSchemaResult? schemaResult = ValidateSchema();
                var hasInstalledVersion = schemaResult?.DetermineHasInstalledVersion() ?? false;

                //If the determined version is "empty" its a new install - otherwise upgrade the existing
                if (!hasInstalledVersion)
                {
                    if (_runtimeState.Level == RuntimeLevel.Run)
                    {
                        throw new Exception("Umbraco is already configured!");
                    }

                    DatabaseSchemaCreator creator = _databaseSchemaCreatorFactory.Create(database);
                    creator.InitializeDatabaseSchema();

                    message += "<p>Installation completed!</p>";

                    //now that everything is done, we need to determine the version of SQL server that is executing
                    _logger.LogInformation("Database configuration status: {DbConfigStatus}", message);
                    return new Result { Message = message, Success = true, Percentage = "100" };
                }

                //we need to do an upgrade so return a new status message and it will need to be done during the next step
                _logger.LogInformation("Database requires upgrade");
                message = "<p>Upgrading database, this may take some time...</p>";
                return new Result
                {
                    RequiresUpgrade = true,
                    Message = message,
                    Success = true,
                    Percentage = "30"
                };
            }
            catch (Exception ex)
            {
                return HandleInstallException(ex);
            }
        }

        [Obsolete("Use UpgradeSchemaAndDataAsync instead. Scheduled for removal in Umbraco 18.")]
        public Result? UpgradeSchemaAndData(UmbracoPlan plan) => UpgradeSchemaAndData((MigrationPlan)plan);

        [Obsolete("Use UpgradeSchemaAndDataAsync instead. Scheduled for removal in Umbraco 18.")]
        public Result? UpgradeSchemaAndData(MigrationPlan plan) => UpgradeSchemaAndDataAsync(plan).GetAwaiter().GetResult();

        public async Task<Result?> UpgradeSchemaAndDataAsync(UmbracoPlan plan) => await UpgradeSchemaAndDataAsync((MigrationPlan)plan).ConfigureAwait(false);

        /// <summary>
        /// Upgrades the database schema and data by running migrations.
        /// </summary>
        /// <remarks>
        /// <para>This assumes that the database exists and the connection string is
        /// configured and it is possible to connect to the database.</para>
        /// <para>Runs whichever migrations need to run.</para>
        /// </remarks>
        public async Task<Result?> UpgradeSchemaAndDataAsync(MigrationPlan plan)
        {
            try
            {
                Attempt<Result?> readyForInstall = CheckReadyForInstall();
                if (readyForInstall.Success == false)
                {
                    return readyForInstall.Result;
                }

                _logger.LogInformation("Database upgrade started");

                // upgrade
                var upgrader = new Upgrader(plan);
                ExecutedMigrationPlan result = await upgrader.ExecuteAsync(_migrationPlanExecutor, _scopeProvider, _keyValueService).ConfigureAwait(false);

                _aggregator.Publish(new UmbracoPlanExecutedNotification { ExecutedPlan = result });

                // The migration may have failed, it this is the case, we throw the exception now that we've taken care of business.
                if (result.Successful is false && result.Exception is not null)
                {
                    return HandleInstallException(result.Exception);
                }

                var message = "<p>Upgrade completed!</p>";
                _logger.LogInformation("Database configuration status: {DbConfigStatus}", message);

                return new Result { Message = message, Success = true, Percentage = "100" };
            }
            catch (Exception ex)
            {
                return HandleInstallException(ex);
            }
        }

        private Attempt<Result?> CheckReadyForInstall()
        {
            if (_databaseFactory.CanConnect == false)
            {
                return Attempt.Fail(new Result
                {
                    Message = "Database configuration is invalid. Please check that the entered database exists and"
                              + " that the provided username and password has write access to the database.",
                    Success = false,
                    Percentage = "10"
                });
            }
            return Attempt<Result?>.Succeed();
        }

        private Result HandleInstallException(Exception ex)
        {
            _logger.LogError(ex, "Database configuration failed");

            if (_databaseSchemaValidationResult != null)
            {
                _logger.LogInformation("The database schema validation produced the following summary: {DbSchemaSummary}", _databaseSchemaValidationResult.GetSummary());
            }

            return new Result
            {
                Message =
                    "The database configuration failed with the following message: " + ex.Message +
                    $"\n Please check log file for additional information (can be found in '{nameof(LoggingSettings)}.{nameof(LoggingSettings.Directory)}')",
                Success = false,
                Percentage = "90"
            };
        }

        /// <summary>
        /// Represents the result of a database creation or upgrade.
        /// </summary>
        public class Result
        {
            /// <summary>
            /// Gets or sets a value indicating whether an upgrade is required.
            /// </summary>
            public bool RequiresUpgrade { get; set; }

            /// <summary>
            /// Gets or sets the message returned by the operation.
            /// </summary>
            public string? Message { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the operation succeeded.
            /// </summary>
            public bool Success { get; set; }

            /// <summary>
            /// Gets or sets an install progress pseudo-percentage.
            /// </summary>
            public string? Percentage { get; set; }
        }

        #endregion
    }
}
