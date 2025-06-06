import type { UmbUserKindType } from '../../utils/index.js';
import { UMB_CREATE_USER_MODAL } from './modal/create-user-modal.token.js';
import { UMB_ACTION_EVENT_CONTEXT } from '@umbraco-cms/backoffice/action';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbRequestReloadChildrenOfEntityEvent } from '@umbraco-cms/backoffice/entity-action';
import {
	UmbEntityCreateOptionActionBase,
	type MetaEntityCreateOptionAction,
	type UmbEntityCreateOptionActionArgs,
} from '@umbraco-cms/backoffice/entity-create-option-action';
import { umbOpenModal } from '@umbraco-cms/backoffice/modal';

export interface UmbUserEntityCreateOptionActionBaseArgs
	extends UmbEntityCreateOptionActionArgs<MetaEntityCreateOptionAction> {
	kind: UmbUserKindType;
}

export abstract class UmbUserEntityCreateOptionActionBase extends UmbEntityCreateOptionActionBase {
	#kind: UmbUserKindType;

	constructor(host: UmbControllerHost, args: UmbUserEntityCreateOptionActionBaseArgs) {
		super(host, args);
		this.#kind = args.kind;
	}

	override async execute() {
		await umbOpenModal(this, UMB_CREATE_USER_MODAL, {
			data: {
				user: {
					kind: this.#kind,
				},
			},
		})
			.then(() => {
				this.#requestReloadChildrenOfEntity();
			})
			.catch(async () => {
				// modal is closed after creation instead of navigating to the new user.
				// We therefore need to reload the children of the entity
				this.#requestReloadChildrenOfEntity();
			});
	}

	async #requestReloadChildrenOfEntity() {
		const eventContext = await this.getContext(UMB_ACTION_EVENT_CONTEXT);
		if (!eventContext) throw new Error('Event context not found');
		const event = new UmbRequestReloadChildrenOfEntityEvent({
			entityType: this.args.entityType,
			unique: this.args.unique,
		});

		eventContext.dispatchEvent(event);
	}
}
