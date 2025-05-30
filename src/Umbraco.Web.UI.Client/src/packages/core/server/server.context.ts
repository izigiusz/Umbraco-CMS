import { UMB_SERVER_CONTEXT } from './server.context-token.js';
import type { UmbServerContextConfig } from './types.js';
import { UmbContextBase } from '@umbraco-cms/backoffice/class-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';

export class UmbServerContext extends UmbContextBase {
	#serverUrl: string;
	#backofficePath: string;
	#serverConnection;

	constructor(host: UmbControllerHost, config: UmbServerContextConfig) {
		super(host, UMB_SERVER_CONTEXT.toString());
		this.#serverUrl = config.serverUrl;
		this.#backofficePath = config.backofficePath;
		this.#serverConnection = config.serverConnection;
	}

	getBackofficePath() {
		return this.#backofficePath;
	}

	getServerUrl() {
		return this.#serverUrl;
	}

	getServerConnection() {
		return this.#serverConnection;
	}
}
