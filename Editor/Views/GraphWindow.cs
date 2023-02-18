using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewGraph {
    public class GraphWindow : EditorWindow {

        private GraphController graphController;
        private static GraphWindow window;

        [MenuItem(GraphSettings.menuItemBase+nameof(GraphWindow))]
        public static void Initialize() {
            window = GetWindow<GraphWindow>(nameof(GraphWindow));
            window.wantsMouseMove= true;
            window.Show();
        }

        private void OnGUI() {
            graphController?.Draw();
        }

        private void OnDisable() {
            graphController?.Disable();
        } 

        public static Vector2 screenPosition {
            get {
                if (window == null) {
                    Initialize();
                }
                return window.position.position;
            }
        }

        public static void Redraw() {
            if (window == null) {
                Initialize();
            }
            window.Repaint();
        }

        private void CreateGUI() {
            VisualElement uxmlRoot = GraphSettings.graphDocument.CloneTree();
            rootVisualElement.Add(uxmlRoot);
            uxmlRoot.StretchToParentSize();

            graphController = new GraphController(uxmlRoot);
            rootVisualElement.styleSheets.Add(GraphSettings.graphStylesheetVariables);
            rootVisualElement.styleSheets.Add(GraphSettings.graphStylesheet);

            // re-open the last opened graph
            GraphModel lastLoadedGraph = GraphSettings.LastOpenedGraphModel;
            if (lastLoadedGraph != null) {
                graphController.OpenGraphExternal(lastLoadedGraph);
            }
        }

    }
}
