import type { UmbMemberItemModel } from '../../item/types.js';
import { UmbMemberPickerInputContext } from './input-member.context.js';
import { css, customElement, html, nothing, property, repeat, state } from '@umbraco-cms/backoffice/external/lit';
import { splitStringToArray } from '@umbraco-cms/backoffice/utils';
import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import { UmbFormControlMixin } from '@umbraco-cms/backoffice/validation';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbSorterController } from '@umbraco-cms/backoffice/sorter';
import { UMB_MEMBER_TYPE_ENTITY_TYPE } from '@umbraco-cms/backoffice/member-type';

@customElement('umb-input-member')
export class UmbInputMemberElement extends UmbFormControlMixin<string | undefined, typeof UmbLitElement>(
	UmbLitElement,
) {
	#sorter = new UmbSorterController<string>(this, {
		getUniqueOfElement: (element) => {
			return element.id;
		},
		getUniqueOfModel: (modelEntry) => {
			return modelEntry;
		},
		identifier: 'Umb.SorterIdentifier.InputMember',
		itemSelector: 'umb-entity-item-ref',
		containerSelector: 'uui-ref-list',
		onChange: ({ model }) => {
			this.selection = model;
			this.dispatchEvent(new UmbChangeEvent());
		},
	});

	/**
	 * This is a minimum amount of selected items in this input.
	 * @type {number}
	 * @attr
	 * @default
	 */
	@property({ type: Number })
	public set min(value: number) {
		this.#pickerContext.min = value;
	}
	public get min(): number {
		return this.#pickerContext.min;
	}

	/**
	 * Min validation message.
	 * @type {boolean}
	 * @attr
	 * @default
	 */
	@property({ type: String, attribute: 'min-message' })
	minMessage = 'This field need more items';

	/**
	 * This is a maximum amount of selected items in this input.
	 * @type {number}
	 * @attr
	 * @default
	 */
	@property({ type: Number })
	public set max(value: number) {
		this.#pickerContext.max = value;
	}
	public get max(): number {
		return this.#pickerContext.max;
	}

	/**
	 * Max validation message.
	 * @type {boolean}
	 * @attr
	 * @default
	 */
	@property({ type: String, attribute: 'max-message' })
	maxMessage = 'This field exceeds the allowed amount of items';

	public set selection(ids: Array<string>) {
		this.#pickerContext.setSelection(ids);
		this.#sorter.setModel(ids);
	}
	public get selection(): Array<string> {
		return this.#pickerContext.getSelection();
	}

	@property({ type: Array })
	allowedContentTypeIds?: string[] | undefined;

	@property({ type: String })
	public override set value(selectionString: string | undefined) {
		this.selection = splitStringToArray(selectionString);
	}
	public override get value(): string | undefined {
		return this.selection.length > 0 ? this.selection.join(',') : undefined;
	}

	@property({ type: Object, attribute: false })
	public filter: (member: UmbMemberItemModel) => boolean = () => true;

	/**
	 * Sets the input to readonly mode, meaning value cannot be changed but still able to read and select its content.
	 * @type {boolean}
	 * @attr
	 * @default
	 */
	@property({ type: Boolean, reflect: true })
	public get readonly() {
		return this.#readonly;
	}
	public set readonly(value) {
		this.#readonly = value;

		if (this.#readonly) {
			this.#sorter.disable();
		} else {
			this.#sorter.enable();
		}
	}
	#readonly = false;

	@state()
	private _items?: Array<UmbMemberItemModel>;

	#pickerContext = new UmbMemberPickerInputContext(this);

	constructor() {
		super();

		this.addValidator(
			'rangeUnderflow',
			() => this.minMessage,
			() => !!this.min && this.selection.length < this.min,
		);

		this.addValidator(
			'rangeOverflow',
			() => this.maxMessage,
			() => !!this.max && this.selection.length > this.max,
		);

		this.observe(this.#pickerContext.selection, (selection) => (this.value = selection.join(',')), '_observeSelection');
		this.observe(this.#pickerContext.selectedItems, (selectedItems) => (this._items = selectedItems), '_observeItems');
	}

	#openPicker() {
		this.#pickerContext.openPicker(
			{
				filter: this.filter,
			},
			{
				allowedContentTypes: this.allowedContentTypeIds?.map((id) => ({
					unique: id,
					entityType: UMB_MEMBER_TYPE_ENTITY_TYPE,
				})),
			},
		);
	}

	#onRemove(item: UmbMemberItemModel) {
		this.#pickerContext.requestRemoveItem(item.unique);
	}

	override render() {
		return html`${this.#renderItems()} ${this.#renderAddButton()}`;
	}

	#renderItems() {
		if (!this._items) return nothing;
		return html`
			<uui-ref-list>
				${repeat(
					this._items,
					(item) => item.unique,
					(item) => this.#renderItem(item),
				)}
			</uui-ref-list>
		`;
	}

	#renderItem(item: UmbMemberItemModel) {
		if (!item.unique) return nothing;
		return html`
			<umb-entity-item-ref id=${item.unique} .item=${item} ?readonly=${this.readonly} ?standalone=${this.max === 1}>
				<uui-action-bar slot="actions">${this.#renderRemoveButton(item)} </uui-action-bar>
			</umb-entity-item-ref>
		`;
	}

	#renderAddButton() {
		if (this.selection.length >= this.max) return nothing;
		if (this.readonly && this.selection.length > 0) {
			return nothing;
		} else {
			return html`
				<uui-button
					id="btn-add"
					look="placeholder"
					@click=${this.#openPicker}
					label=${this.localize.term('general_choose')}
					?disabled=${this.readonly}></uui-button>
			`;
		}
	}

	#renderRemoveButton(item: UmbMemberItemModel) {
		if (this.readonly) return nothing;
		return html`
			<uui-button @click=${() => this.#onRemove(item)} label=${this.localize.term('general_remove')}></uui-button>
		`;
	}

	static override styles = [
		css`
			#btn-add {
				display: block;
			}

			umb-entity-item-ref[drag-placeholder] {
				opacity: 0.2;
			}
		`,
	];
}

export { UmbInputMemberElement as element };

declare global {
	interface HTMLElementTagNameMap {
		'umb-input-member': UmbInputMemberElement;
	}
}
