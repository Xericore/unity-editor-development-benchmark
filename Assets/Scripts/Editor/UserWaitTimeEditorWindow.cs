using System.Collections.Generic;
using System.Linq;
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
            var root = rootVisualElement;
            
            RefreshAndDrawData(root);
            
            UserWaitTimeAggregator.Instance.AnyWaitEventFired += () => RefreshAndDrawData(root);
        }

        private void RefreshAndDrawData(VisualElement root)
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
            
            var resetTotalButton = new Button
            {
                name = "resetTotalButton",
                text = "Reset Total",
                style = {maxWidth = new StyleLength(160)},
            };
            
            resetTotalButton.clicked += () =>
            {
                UserWaitTimeAggregator.Instance.ResetTotalWaitTime();
            };
            
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
                title = "Last Wait Time [mm:ss.ff]",
                width = 120,
                makeCell = () => new Label(),
                bindCell = (element, i) => ((Label) element).text =
                    $@"{_waitTimeDatas[i].WaitTime:mm\:ss\.ff}"
            });
            
            table.columns.Add(new Column
            {
                title = "Total Wait Time [hh:mm:ss]",
                width = 120,
                makeCell = () => new Label(),
                bindCell = (element, i) => ((Label) element).text =
                    $@"{_waitTimeDatas[i].TotalWaitTime:hh\:mm\:ss}"
            });
            
            table.columns.Add(new Column
            {
                title = "Ratio (Total)",
                width = 120,
                makeCell = () => new Label(),
                bindCell = (element, i) => ((Label) element).text = 
                    $"{_waitTimeDatas[i].TotalWaitTime.TotalMilliseconds / _waitTimeDatas.Last().TotalWaitTime.TotalMilliseconds:P1}"
            });
            
            
            table.columns.Add(new Column
            {
                title = "Ratio (Total)",
                width = 120,
                makeCell = () => new ProgressBar(),
                bindCell = (element, i) => ((ProgressBar) element).value = (float)(_waitTimeDatas[i].TotalWaitTime.TotalMilliseconds / _waitTimeDatas.Last().TotalWaitTime.TotalMilliseconds)*100f
            });

            root.Add(table);
        }
    }
}