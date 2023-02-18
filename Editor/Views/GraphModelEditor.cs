using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;

namespace NewGraph {
    [CustomEditor(typeof(GraphModel))]
    public class GraphModelEditor : Editor {
        SerializedProperty listProperty;

        public override VisualElement CreateInspectorGUI() {
            VisualElement inspector = new VisualElement();
            inspector.AddToClassList("baseGraphEditor");

            Button openGraphButton = new Button(OpenGraphClicked) { text = GraphSettings.Instance.openGraphButtonText };
            openGraphButton.Add(GraphSettings.LoadButtonIcon);
            inspector.Add(openGraphButton);
            inspector.styleSheets.Add(GraphSettings.graphStylesheetVariables);
            inspector.styleSheets.Add(GraphSettings.graphStylesheet);

            listProperty = serializedObject.FindProperty(nameof(GraphModel.nodes));
            ListView listView= new ListView() {
                showAddRemoveFooter=false,
                reorderable = false,
                showFoldoutHeader = false,
                showBorder = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.All,
                bindingPath = listProperty.propertyPath,
                bindItem = BindItem,
                makeItem = MakeItem
            };
            inspector.Add(listView);

            return inspector;
        }

        private VisualElement MakeItem() {
            VisualElement itemRow = new VisualElement();
            Label fieldLabel = new Label();
            fieldLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            itemRow.style.flexDirection = FlexDirection.Row;
            itemRow.Add(fieldLabel);
            itemRow.SetEnabled(false);
            return itemRow;
        }

        private void BindItem(VisualElement itemRow, int i) {
            SerializedProperty prop = listProperty.GetArrayElementAtIndex(i);
            Label label = itemRow[0] as Label;
            label.text = $"Element {i+1}: {prop.FindPropertyRelative(NodeModel.nameIdentifier).stringValue}";
        }

        private void OpenGraphClicked() {
            OpenGraph(target as GraphModel);
        }

        private static void OpenGraph(GraphModel graphModel) {
            GraphSettings.LastOpenedGraphModel = graphModel;
            GraphWindow.Initialize();
        }

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line) {
            GraphModel baseGraphModel = EditorUtility.InstanceIDToObject(instanceID) as GraphModel;
            if (baseGraphModel != null) {
                OpenGraph(baseGraphModel);
                return true;
            }
            return false;
        }
    }
}

