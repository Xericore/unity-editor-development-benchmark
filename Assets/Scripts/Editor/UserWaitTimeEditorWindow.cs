using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    public class UserWaitTimeEditorWindow : EditorWindow
    {
        private List<UserWaitTimeData> _waitTimeDatas;

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
            
            var refreshUIButton = new Button
            {
                name = "refreshUIButton",
                text = "Refresh UI",
                style = {maxWidth = new StyleLength(160)},
            };            
            
            var resetTotalButton = new Button
            {
                name = "resetTotalButton",
                text = "Reset Total",
                style = {maxWidth = new StyleLength(160)},
            };
            
            refreshUIButton.clicked += () =>
            {
                RefreshAndDrawData(root, refreshUIButton, resetTotalButton);
            };
            resetTotalButton.clicked += () =>
            {
                UserWaitTimeAggregator.Instance.ResetTotalWaitTime();
            };
            
            RefreshAndDrawData(root, refreshUIButton, resetTotalButton);
            
            UserWaitTimeAggregator.Instance.AnyWaitEventFired += () => RefreshAndDrawData(root, refreshUIButton, resetTotalButton);
        }

        private void RefreshAndDrawData(VisualElement root, VisualElement refreshUIButton,
            VisualElement resetTotalButton)
        {
            _waitTimeDatas = UserWaitTimeAggregator.Instance.GetWaitTimeData();
            
            root.Clear();
            
            var buttonRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };
            buttonRow.Add(refreshUIButton);
            buttonRow.Add(resetTotalButton);

            root.Add(buttonRow);
            
            CreateTable(root);
        }

        private void CreateTable(VisualElement root)
        {
            var table = new MultiColumnListView
            {
                itemsSource = _waitTimeDatas,
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
                    label.text = _waitTimeDatas[i].Name;
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
                    $@"{_waitTimeDatas[i].WaitTime:mm\:ss\.ff}"
            });
            
            table.columns.Add(new Column
            {
                title = "Total Wait Time",
                width = 120,
                makeCell = () => new Label(),
                bindCell = (element, i) => ((Label) element).text =
                    $@"{_waitTimeDatas[i].TotalWaitTime:mm\:ss\.ff}"
            });

            root.Add(table);
        }
    }
}