import type { UmbDocumentVariantOptionModel } from '../../../types.js';
import { UMB_DOCUMENT_UNPUBLISH_MODAL } from '../constants.js';
import { UmbDocumentDetailRepository } from '../../../repository/index.js';
import { UmbDocumentPublishingRepository } from '../../repository/index.js';
import { UMB_APP_LANGUAGE_CONTEXT, UmbLanguageCollectionRepository } from '@umbraco-cms/backoffice/language';
import {
	type UmbEntityActionArgs,
	UmbEntityActionBase,
	UmbRequestReloadStructureForEntityEvent,
} from '@umbraco-cms/backoffice/entity-action';
import { UmbVariantId } from '@umbraco-cms/backoffice/variant';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { umbOpenModal } from '@umbraco-cms/backoffice/modal';
import { UMB_ACTION_EVENT_CONTEXT } from '@umbraco-cms/backoffice/action';
import { UMB_CURRENT_USER_CONTEXT } from '@umbraco-cms/backoffice/current-user';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';

export class UmbUnpublishDocumentEntityAction extends UmbEntityActionBase<never> {
	constructor(host: UmbControllerHost, args: UmbEntityActionArgs<never>) {
		super(host, args);
	}

	override async execute() {
		if (!this.args.unique) throw new Error('The document unique identifier is missing');

		const notificationContext = await this.getContext(UMB_NOTIFICATION_CONTEXT);
		const localize = new UmbLocalizationController(this);

		const languageRepository = new UmbLanguageCollectionRepository(this._host);
		const { data: languageData } = await languageRepository.requestCollection({});

		const documentRepository = new UmbDocumentDetailRepository(this._host);
		const { data: documentData } = await documentRepository.requestByUnique(this.args.unique);

		if (!documentData) throw new Error('The document was not found');

		const appLanguageContext = await this.getContext(UMB_APP_LANGUAGE_CONTEXT);
		if (!appLanguageContext) throw new Error('The app language context is missing');
		const appCulture = appLanguageContext.getAppCulture();

		const currentUserContext = await this.getContext(UMB_CURRENT_USER_CONTEXT);
		if (!currentUserContext) throw new Error('The current user context is missing');
		const currentUserAllowedLanguages = currentUserContext.getLanguages();
		const currentUserHasAccessToAllLanguages = currentUserContext.getHasAccessToAllLanguages();

		if (currentUserAllowedLanguages === undefined) throw new Error('The current user languages are missing');
		if (currentUserHasAccessToAllLanguages === undefined)
			throw new Error('The current user access to all languages is missing');

		const cultureVariantOptions = documentData.variants.filter((variant) => variant.segment === null);

		const options: Array<UmbDocumentVariantOptionModel> = cultureVariantOptions.map<UmbDocumentVariantOptionModel>(
			(variant) => ({
				culture: variant.culture,
				segment: variant.segment,
				language: languageData?.items.find((language) => language.unique === variant.culture) ?? {
					name: appCulture!,
					entityType: 'language',
					fallbackIsoCode: null,
					isDefault: true,
					isMandatory: false,
					unique: appCulture!,
				},
				variant,
				unique: new UmbVariantId(variant.culture, variant.segment).toString(),
			}),
		);

		// Figure out the default selections
		// TODO: Missing features to pre-select the variant that fits with the variant-id of the tree/collection? (Again only relevant if the action is executed from a Tree or Collection) [NL]
		const selection: Array<string> = [];
		// If the app language is one of the options, select it by default:
		if (appCulture && options.some((o) => o.unique === appCulture)) {
			selection.push(new UmbVariantId(appCulture, null).toString());
		} else {
			// If not, select the first option by default:
			selection.push(options[0].unique);
		}

		const result = await umbOpenModal(this, UMB_DOCUMENT_UNPUBLISH_MODAL, {
			data: {
				documentUnique: this.args.unique,
				options,
				pickableFilter: (option) => {
					if (!option.culture) return false;
					if (currentUserHasAccessToAllLanguages) return true;
					return currentUserAllowedLanguages.includes(option.culture);
				},
			},
			value: { selection },
		}).catch(() => undefined);

		if (!result?.selection.length) return;

		const variantIds = result?.selection.map((x) => UmbVariantId.FromString(x)) ?? [];

		if (!variantIds.length) return;

		const publishingRepository = new UmbDocumentPublishingRepository(this._host);
		const { error } = await publishingRepository.unpublish(this.args.unique, variantIds);

		if (error) {
			throw error;
		}

		if (!error) {
			// If the content is invariant, we need to show a different notification
			const isInvariant = options.length === 1 && options[0].culture === null;

			if (isInvariant) {
				notificationContext?.peek('positive', {
					data: {
						headline: localize.term('speechBubbles_editContentUnpublishedHeader'),
						message: localize.term('speechBubbles_editContentUnpublishedText'),
					},
				});
			} else {
				const documentVariants = documentData.variants.filter((variant) => result.selection.includes(variant.culture!));
				notificationContext?.peek('positive', {
					data: {
						headline: localize.term('speechBubbles_editContentUnpublishedHeader'),
						message: localize.term(
							'speechBubbles_editVariantUnpublishedText',
							localize.list(documentVariants.map((v) => v.culture ?? v.name)),
						),
					},
				});
			}

			const actionEventContext = await this.getContext(UMB_ACTION_EVENT_CONTEXT);
			const event = new UmbRequestReloadStructureForEntityEvent({
				unique: this.args.unique,
				entityType: this.args.entityType,
			});

			actionEventContext?.dispatchEvent(event);
		}
	}
}

export default UmbUnpublishDocumentEntityAction;
