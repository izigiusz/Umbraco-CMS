import type {
	ManifestTiptapExtension,
	ManifestTiptapToolbarExtension,
	UmbTiptapExtensionApi,
	UmbTiptapExtensionArgs,
	UmbTiptapToolbarElementApi,
} from './types.js';
import { UmbControllerBase } from '@umbraco-cms/backoffice/class-api';
import type { CSSResultGroup } from '@umbraco-cms/backoffice/external/lit';
import type { Editor, Extension, Mark, Node } from '@umbraco-cms/backoffice/external/tiptap';
import type { UmbPropertyEditorConfigCollection } from '@umbraco-cms/backoffice/property-editor';

export abstract class UmbTiptapExtensionApiBase extends UmbControllerBase implements UmbTiptapExtensionApi {
	/**
	 * The manifest for the extension.
	 */
	manifest?: ManifestTiptapExtension;

	/**
	 * The editor instance.
	 */
	protected _editor?: Editor;

	/**
	 * @inheritdoc
	 */
	setEditor(editor: Editor): void {
		this._editor = editor;
	}

	/**
	 * @inheritdoc
	 */
	getStyles(): CSSResultGroup | null | undefined {
		return null;
	}

	/**
	 * @inheritdoc
	 */
	abstract getTiptapExtensions(args?: UmbTiptapExtensionArgs): Array<Extension | Mark | Node>;
}

export abstract class UmbTiptapToolbarElementApiBase extends UmbControllerBase implements UmbTiptapToolbarElementApi {
	#enabledExtensions?: Array<string>;

	/**
	 * The manifest for the extension.
	 */
	manifest?: ManifestTiptapToolbarExtension;

	/**
	 * The data type configuration for the property editor that the editor is used for.
	 */
	configuration?: UmbPropertyEditorConfigCollection;

	/**
	 * A method to execute the toolbar element action.
	 * @see {ManifestTiptapToolbarExtension}
	 * @param {Editor} editor The editor instance.
	 */
	public abstract execute(editor?: Editor): void;

	/**
	 * Informs the toolbar element if it is active or not. It uses the manifest meta alias to check if the toolbar element is active.
	 * @see {ManifestTiptapToolbarExtension}
	 * @param {Editor} editor The editor instance.
	 * @returns {boolean} Returns true if the toolbar element is active.
	 */
	public isActive(editor?: Editor): boolean {
		return editor && this.manifest?.meta.alias ? editor?.isActive(this.manifest.meta.alias) === true : false;
	}

	/**
	 * Informs the toolbar element if it is disabled or not.
	 * @see {ManifestTiptapToolbarExtension}
	 * @param {Editor} editor The editor instance.
	 * @returns {boolean} Returns true if the toolbar element is disabled.
	 */
	isDisabled(editor?: Editor): boolean {
		if (!editor) return true;
		if (!this.#enabledExtensions) {
			this.#enabledExtensions = this.configuration?.getValueByAlias<string[]>('extensions') ?? [];
		}
		return this.manifest?.forExtensions?.every((ext) => this.#enabledExtensions?.includes(ext)) === false;
	}
}
