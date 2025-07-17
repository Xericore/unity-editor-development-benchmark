using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    public class UserWaitTimeEditorWindow : EditorWindow
    {
        private List<UserWaitTimeData> _lastWaitTimeData;

        [MenuItem("Window/Analysis/User Wait Time Editor Window")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<UserWaitTimeEditorWindow>();
            wnd.titleContent = new GUIContent("User Wait Time Editor Window");
        }
        
        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            var root = rootVisualElement;
            
            // VisualElements objects can contain other VisualElement following a tree hierarchy
            
            var button = new Button
            {
                name = "button",
                text = "Refresh",
                style = {maxWidth = new StyleLength(160)},
            };
            
            button.clicked += () =>
            {
                RefreshAndDrawData(root, button);
            };
            
            RefreshAndDrawData(root, button);
            
            UserWaitTimeAggregator.Instance.AnyWaitEventFired += () => RefreshAndDrawData(root, button);;
        }

        private void RefreshAndDrawData(VisualElement root, VisualElement button)
        {
            _lastWaitTimeData = UserWaitTimeAggregator.Instance.GetWaitTimeData();
            root.Clear();
            root.Add(button);
            CreateTable(root);
        }

        private void CreateTable(VisualElement root)
        {
            var table = new MultiColumnListView
            {
                itemsSource = _lastWaitTimeData,
                showBoundCollectionSize = false,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                style = {flexGrow = 1}
            };
            
            table.columns.Add(new Column
            {
                title = "Name",
                width = 120,
                makeCell = () => new Label(),
                bindCell = (element, i) =>
                {
                    var label = (Label) element;
                    label.text = _lastWaitTimeData[i].Name;
                    label.style.paddingLeft = 8;

                    if (!label.text.Contains("Total"))
                        return;
                    
                    label.style.unityFontStyleAndWeight = FontStyle.Bold;
                }
            });

            table.columns.Add(new Column
            {
                title = "Last Wait Time",
                width = 120,
                makeCell = () => new Label(),
                bindCell = (element, i) => ((Label) element).text =
                    $@"{_lastWaitTimeData[i].WaitTime:mm\:ss\.ff}"
            });

            root.Add(table);
        }
    }
}