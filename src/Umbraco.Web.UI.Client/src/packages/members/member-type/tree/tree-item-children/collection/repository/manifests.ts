import { UMB_MEMBER_TYPE_TREE_ITEM_CHILDREN_COLLECTION_REPOSITORY_ALIAS } from './constants.js';

export const manifests: Array<UmbExtensionManifest> = [
	{
		type: 'repository',
		alias: UMB_MEMBER_TYPE_TREE_ITEM_CHILDREN_COLLECTION_REPOSITORY_ALIAS,
		name: 'Member Type Tree Item Children Collection Repository',
		api: () => import('./member-type-tree-item-children-collection.repository.js'),
	},
];
