import type { UmbCurrentUserAction, UmbCurrentUserActionArgs } from '../current-user-action.extension.js';
import { UMB_CURRENT_USER_EXTERNAL_LOGIN_MODAL } from './modals/external-login-modal.token.js';
import { UmbActionBase } from '@umbraco-cms/backoffice/action';
import { umbOpenModal } from '@umbraco-cms/backoffice/modal';

export class UmbConfigureExternalLoginProvidersApi<ArgsMetaType = never>
	extends UmbActionBase<UmbCurrentUserActionArgs<ArgsMetaType>>
	implements UmbCurrentUserAction<ArgsMetaType>
{
	async getHref() {
		return undefined;
	}

	async execute() {
		await umbOpenModal(this, UMB_CURRENT_USER_EXTERNAL_LOGIN_MODAL);
	}
}

export { UmbConfigureExternalLoginProvidersApi as api };
