import type { UmbImageCropChangeEvent } from './crop-change.event.js';
import type { UmbFocalPointChangeEvent } from './focalpoint-change.event.js';
import type { UmbImageCropperElement } from './image-cropper.element.js';
import type {
	UmbImageCropperCrop,
	UmbImageCropperCrops,
	UmbImageCropperFocalPoint,
	UmbImageCropperPropertyEditorValue,
} from './types.js';
import { css, customElement, html, property, repeat, state, when } from '@umbraco-cms/backoffice/external/lit';
import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_SERVER_CONTEXT } from '@umbraco-cms/backoffice/server';

@customElement('umb-image-cropper-field')
export class UmbInputImageCropperFieldElement extends UmbLitElement {
	@property({ attribute: false })
	set value(value) {
		if (!value) {
			this.crops = [];
			this.focalPoint = { left: 0.5, top: 0.5 };
			this.src = '';
			this.#value = undefined;
		} else {
			this.crops = [...value.crops];
			// TODO: This is a temporary solution to make sure we have a focal point
			this.focalPoint = value.focalPoint || { left: 0.5, top: 0.5 };
			this.src = value.src;
			this.#value = value;
		}

		this.requestUpdate();
	}
	get value() {
		return this.#value;
	}
	#value?: UmbImageCropperPropertyEditorValue;

	@state()
	crops: UmbImageCropperCrops = [];

	@state()
	currentCrop?: UmbImageCropperCrop;

	@property({ attribute: false })
	set file(file: File | undefined) {
		this.#file = file;
		if (file) {
			this.fileDataUrl = URL.createObjectURL(file);
		} else if (this.fileDataUrl) {
			URL.revokeObjectURL(this.fileDataUrl);
			this.fileDataUrl = undefined;
		}
	}
	get file() {
		return this.#file;
	}
	#file?: File;

	@property()
	fileDataUrl?: string;

	@state()
	focalPoint: UmbImageCropperFocalPoint = { left: 0.5, top: 0.5 };

	@property({ type: Boolean })
	hideFocalPoint = false;

	@state()
	src = '';

	@state()
	private _serverUrl = '';

	get source(): string {
		if (this.src) {
			// Test that URL is relative:
			if (this.src.startsWith('/')) {
				return `${this._serverUrl}${this.src}`;
			} else {
				return this.src;
			}
		}

		return this.fileDataUrl ?? '';
	}

	constructor() {
		super();

		this.consumeContext(UMB_SERVER_CONTEXT, (context) => {
			this._serverUrl = context?.getServerUrl() ?? '';
		});
	}

	override disconnectedCallback(): void {
		super.disconnectedCallback();
		if (this.fileDataUrl) {
			URL.revokeObjectURL(this.fileDataUrl);
		}
	}

	protected onCropClick(crop: any) {
		const index = this.crops.findIndex((c) => c.alias === crop.alias);

		if (index === -1) return;

		this.currentCrop = { ...this.crops[index] };
	}

	#onCropChange = (event: UmbImageCropChangeEvent) => {
		const target = event.target as UmbImageCropperElement;
		const value = target.value;

		if (!value) return;

		const index = this.crops.findIndex((crop) => crop.alias === value.alias);

		if (index === undefined) return;

		this.crops[index] = value;
		this.currentCrop = undefined;
		this.#updateValue();
	};

	#onFocalPointChange = (event: UmbFocalPointChangeEvent) => {
		this.focalPoint = { top: event.focalPoint.top, left: event.focalPoint.left };
		this.#updateValue();
	};

	#updateValue() {
		this.#value = {
			crops: [...this.crops],
			focalPoint: this.focalPoint,
			src: this.src,
		};

		this.dispatchEvent(new UmbChangeEvent());
	}

	protected onResetFocalPoint = () => {
		this.focalPoint = { left: 0.5, top: 0.5 };
		this.#updateValue();
	};

	override render() {
		return html`
			<div id="main">${this.renderMain()}</div>
			<div id="side">${this.renderSide()}</div>
		`;
	}

	protected renderMain() {
		if (this.currentCrop) {
			return html`
				<umb-image-cropper
					.focalPoint=${this.focalPoint}
					.src=${this.source}
					.value=${this.currentCrop}
					?hideFocalPoint=${this.hideFocalPoint}
					@imagecrop-change=${this.#onCropChange}>
				</umb-image-cropper>
			`;
		}

		return html`
			<umb-image-cropper-focus-setter
				.focalPoint=${this.focalPoint}
				.src=${this.source}
				?hideFocalPoint=${this.hideFocalPoint}
				@focalpoint-change=${this.#onFocalPointChange}>
			</umb-image-cropper-focus-setter>
			<div id="actions">${this.renderActions()}</div>
		`;
	}

	protected renderActions() {
		return html`<slot name="actions"></slot>
			${when(
				!this.hideFocalPoint,
				() =>
					html`<uui-button
						label=${this.localize.term('content_resetFocalPoint')}
						@click=${this.onResetFocalPoint}></uui-button>`,
			)} `;
	}

	protected renderSide() {
		if (!this.value || !this.crops) return;

		return repeat(
			this.crops,
			(crop) => crop.alias + JSON.stringify(crop.coordinates),
			(crop) =>
				html` <umb-image-cropper-preview
					@click=${() => this.onCropClick(crop)}
					.crop=${crop}
					.focalPoint=${this.focalPoint}
					.src=${this.source}></umb-image-cropper-preview>`,
		);
	}
	static override styles = css`
		:host {
			display: flex;
			width: 100%;
			box-sizing: border-box;
			gap: var(--uui-size-space-3);
			height: 400px;
		}

		#main {
			max-width: 500px;
			min-width: 300px;
			width: 100%;
			height: 100%;
			display: flex;
			gap: var(--uui-size-space-1);
			flex-direction: column;
		}

		#actions {
			display: flex;
			justify-content: space-between;
			margin-top: 0.5rem;
		}

		umb-image-cropper-focus-setter {
			height: calc(100% - 33px - var(--uui-size-space-1)); /* Temp solution to make room for actions */
		}

		#side {
			display: grid;
			grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
			gap: var(--uui-size-space-3);
			flex-grow: 1;
			overflow-y: auto;
			height: fit-content;
			max-height: 100%;
		}
	`;
}

declare global {
	interface HTMLElementTagNameMap {
		'umb-image-cropper-field': UmbInputImageCropperFieldElement;
	}
}
