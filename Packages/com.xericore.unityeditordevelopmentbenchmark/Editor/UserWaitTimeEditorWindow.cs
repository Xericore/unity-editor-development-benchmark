using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    public class UserWaitTimeEditorWindow : EditorWindow
    {
        private List<UserWaitTimeData> _allWaitTimeData;

        [MenuItem("Window/Analysis/User Wait Time Tracker")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<UserWaitTimeEditorWindow>();
            wnd.titleContent = new GUIContent("User Wait Time");
        }
        
        public void CreateGUI()
        {
            var root = rootVisualElement;
            
            RefreshAndDrawData(root);
            
            UserWaitTimeAggregator.AnyWaitEventFired += () => RefreshAndDrawData(root);
        }

        private void RefreshAndDrawData(VisualElement root)
        {
            _allWaitTimeData = UserWaitTimeAggregator.GetWaitTimeData();
            
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
            
            resetTotalButton.clicked += UserWaitTimeAggregator.ResetTotalWaitTime;
            
            buttonRow.Add(resetTotalButton);

            root.Add(buttonRow);
            
            CreateTable(root);
        }

        private void CreateTable(VisualElement root)
        {
            var table = new MultiColumnListView
            {
                itemsSource = _allWaitTimeData,
                showBoundCollectionSize = false,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                style = {flexGrow = 1}
            };
            
            table.columns.Add(new Column
            {
                title = "Category",
                width = 200,
                makeCell = () => new Label(),
                bindCell = (element, i) =>
                {
                    var label = (Label) element;
                    label.text = _allWaitTimeData[i].Name;
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
                bindCell = (element, i) =>
                {
                    var text = CustomFormatTimeSpan(_allWaitTimeData[i].WaitTime);
                    CustomStyledLabel(element, text);
                }
            });
            
            table.columns.Add(new Column
            {
                title = "Ratio (Total)",
                width = 120,
                makeCell = () => new ProgressBar(),
                bindCell = (element, i) =>
                {
                    var progressBar = ((ProgressBar) element);
                    progressBar.value = (float) (_allWaitTimeData[i].TotalWaitTime.TotalMilliseconds /
                                                 _allWaitTimeData.Last().TotalWaitTime
                                                     .TotalMilliseconds) * 100f;

                    var percentage =
                        $"{_allWaitTimeData[i].TotalWaitTime.TotalMilliseconds / _allWaitTimeData.Last().TotalWaitTime.TotalMilliseconds:P1}";
                    progressBar.title = percentage;
                }
            });
            
            table.columns.Add(new Column
            {
                title = "Total Wait Time",
                width = 120,
                makeCell = () => new Label(),
                bindCell = (element, i) =>
                {
                    var text = CustomFormatTimeSpan(_allWaitTimeData[i].TotalWaitTime);
                    CustomStyledLabel(element, text);
                }
            });
            
            root.Add(table);
        }

        private static void CustomStyledLabel(VisualElement element, string text)
        {
            var label = (Label) element;
            label.style.paddingLeft = 8;
            label.text = text;
        }

        private static string CustomFormatTimeSpan(TimeSpan waitTime)
        {
            return $"{waitTime.Hours}h {waitTime.Minutes}m {waitTime.Seconds}s";
        }
    }
}