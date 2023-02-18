using GraphViewBase;
using System;
using System.Collections.Generic;
using UnityEngine;
using static GraphViewBase.GraphView;

namespace NewGraph {
    /// <summary>
    /// A searchable menu window. This is a more sophisticated version of a dropdown menu.
    /// </summary>
    public class GraphSearchWindow : ScriptableObject, ISearchWindowProvider {
        /// <summary>
        /// Node entries need to be pre-processed this helper class allows us to freely add all nodes first and process them later.
        /// </summary>
        public class NodeCreationEntry {
            /// <summary>
            /// Full menu path of the node.
            /// </summary>
            public string fullpath;
            /// <summary>
            /// Action to be executed when the menu item was clicked.
            /// </summary>
            public Action<object> action;
        }

        private ShortcutHandler shortcutHandler;
        private List<SearchTreeEntry> searchTreeEntries = new List<SearchTreeEntry>();
        private List<NodeCreationEntry> nodeEntries = new List<NodeCreationEntry>();

        /// <summary>
        /// Method to be called right after an object of this type was created.
        /// </summary>
        /// <param name="shortcutHandler"></param>
        public void Initialize(ShortcutHandler shortcutHandler) {
            this.shortcutHandler = shortcutHandler;
        }

        /// <summary>
        /// Initiator to start adding menu entries.
        /// </summary>
        /// <param name="header"></param>
        public void StartAddingMenuEntries(string header) {
            searchTreeEntries.Clear();
            nodeEntries.Clear();
            AddGroupEntry(header, false);
        }

        /// <summary>
        /// Called by the SearchWindow
        /// </summary>
        /// <returns></returns>
        public List<SearchTreeEntry> CreateSearchTree() {
            return searchTreeEntries;
        } 

        /// <summary>
        /// Resolve all of the added node entries.
        /// This will build all of the necessary sub menus...
        /// </summary>
        /// <param name="nodeEnabledCheck">The common check if it should be allowed to create nodes.</param>
        public void ResolveNodeEntries(Func<bool> nodeEnabledCheck) {
            // make sure all of our nodes are sorted
            int Compare(NodeCreationEntry x, NodeCreationEntry y) {
                return x.fullpath.CompareTo(y.fullpath);
            }
            nodeEntries.Sort(Compare);

            // build submenus & actual node menu entries
            HashSet<string> menus = new HashSet<string>();
            foreach (NodeCreationEntry entry in nodeEntries) {
                int level = 1;
                // lets go over every path partial
                string[] submenus = entry.fullpath.Split('/');
                if (submenus.Length > 1) {
                    level = 1;
                    string menuNameBuilder = "";
                    for (int i = 0; i < submenus.Length - 1; i++) {
                        menuNameBuilder += submenus[i] + "/";
                        string menuName = submenus[i];
                        // have we already constructed the sub menu?
                        if (!menus.Contains(menuNameBuilder)) {
                            menus.Add(menuNameBuilder);
                            // add a group entry
                            AddGroupEntry(menuName, false, level);
                        }
                        level++;
                    }
                }
                // the last item is always the actual node we need to create an entry for.
                AddEntry(submenus[submenus.Length - 1], nodeEnabledCheck, entry.action, level);
            }
        }

        /// <summary>
        /// Add labeled separator.
        /// </summary>
        /// <param name="label"></param>
        public void AddSeparator(string label) {
            AddGroupEntry(label);
        }

        /// <summary>
        /// Add a group entry aka add a submenu
        /// </summary>
        /// <param name="label"></param>
        /// <param name="asInline">if this is marked as true, we'll just create a headline instead</param>
        /// <param name="level">The level that the subemenu belongs to.</param>
        private void AddGroupEntry(string label, bool asInline=true, int level=0) {
            if (asInline) {
                searchTreeEntries.Add(new InlineHeaderEntry(label) { level = level == 0 ? 1 : level });
            } else {
                searchTreeEntries.Add(new SearchTreeGroupEntry(label, SearchTreeEntry.AlwaysEnabled, level));
            }
        }

        /// <summary>
        /// Add a node to the list of node entries.
        /// </summary>
        /// <param name="fullPath">The full menu path of this node.</param>
        /// <param name="action">The actions that should be executed when this element was clicked.</param>
        public void AddNodeEntry(string fullPath, Action<object> action) {
            nodeEntries.Add(new NodeCreationEntry() { fullpath= fullPath, action= action });
        }

        /// <summary>
        /// Base method to create an SearchTreeEntry
        /// </summary>
        /// <param name="label"></param>
        /// <param name="enabledCheck"></param>
        /// <param name="action"></param>
        /// <param name="level"></param>
        private void AddEntry(string label, Func<bool> enabledCheck, Action<object> action, int level = 1) {
            searchTreeEntries.Add(new SearchTreeEntry(label, enabledCheck, action) { level = level });
        }

        /// <summary>
        /// Add a shortcut entry based on a ShortCut Handler action
        /// </summary>
        /// <param name="actionType"></param>
        /// <param name="enabledCheck"></param>
        /// <param name="action"></param>
        public void AddShortcutEntry(Actions actionType, Func<bool> enabledCheck, Action<object> action) {
            AddEntry(shortcutHandler.GetKeyAction(actionType).DisplayName, enabledCheck, action);
        }
    }
}
