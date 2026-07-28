using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking
{
    /// <summary>
    /// Companion window to <see cref="UserWaitTimeEditorWindow"/>: lets you kick off <see cref="BenchmarkRunner"/>
    /// from a button instead of the menu/command line, and shows the same kind of category/progress-bar table,
    /// but for the last (or currently in-progress) automated benchmark run rather than live accumulated wait
    /// time. Rows are color-coded the same way as <see cref="BenchmarkRunner"/>'s console log breakdown (green
    /// shortest, red longest, relative to the other categories in that run), and a bold "Total" row shows the
    /// overall benchmark duration.
    /// </summary>
    public class BenchmarkRunnerEditorWindow : EditorWindow
    {
        private const string _progressUssClassName = "unity-progress-bar__progress";
        private const long _pollingIntervalMilliseconds = 500;

        private List<BenchmarkCategoryResultData> _results;

        [MenuItem("Window/Analysis/Start Benchmark Window")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<BenchmarkRunnerEditorWindow>();
            wnd.titleContent = new GUIContent("Benchmark Runner");
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            RefreshAndDrawData(root);

            // Refresh immediately when a run finishes...
            BenchmarkRunner.BenchmarkFinished += () => RefreshAndDrawData(root);

            // ...but also poll as a fallback, so the "Start Benchmark" button re-enables (and the table
            // refreshes) even if this window's subscription above got dropped by a domain reload happening at
            // just the wrong moment (e.g. right as the benchmark's own play mode switch, or one of its forced
            // recompilations, is triggering one).
            root.schedule.Execute(() => RefreshAndDrawData(root)).Every(_pollingIntervalMilliseconds);
        }

        private void RefreshAndDrawData(VisualElement root)
        {
            _results = BuildResultData();

            root.Clear();

            var buttonRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            var isRunning = BenchmarkRunner.IsRunning;

            var startBenchmarkButton = new Button
            {
                name = "startBenchmarkButton",
                text = isRunning ? "Benchmark Running..." : "Start Benchmark",
                style = {maxWidth = new StyleLength(200)}
            };

            startBenchmarkButton.SetEnabled(!isRunning);
            startBenchmarkButton.clicked += () =>
            {
                BenchmarkRunner.StartBenchmark();
                RefreshAndDrawData(root);
            };

            buttonRow.Add(startBenchmarkButton);

            root.Add(buttonRow);

            CreateTable(root);
        }

        private void CreateTable(VisualElement root)
        {
            var categoryResults = _results.Where(result => !result.IsTotal).ToList();

            var minSeconds = categoryResults.Count > 0 ? categoryResults.Min(result => result.Duration.TotalSeconds) : 0d;
            var maxSeconds = categoryResults.Count > 0 ? categoryResults.Max(result => result.Duration.TotalSeconds) : 0d;
            var totalSeconds = _results.Last().Duration.TotalSeconds;

            var table = new MultiColumnListView
            {
                itemsSource = _results,
                showBoundCollectionSize = false,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                style = {flexGrow = 1}
            };

            table.columns.Add(new Column
            {
                title = "Category",
                width = 160,
                makeCell = () => new Label(),
                bindCell = (element, i) =>
                {
                    var data = _results[i];
                    var label = (Label) element;
                    label.text = data.Name;
                    label.style.paddingLeft = 8;
                    label.style.unityFontStyleAndWeight = data.IsTotal ? FontStyle.Bold : FontStyle.Normal;
                }
            });

            table.columns.Add(new Column
            {
                title = "Duration",
                width = 130,
                makeCell = () => new Label(),
                bindCell = (element, i) =>
                {
                    var data = _results[i];
                    var label = (Label) element;
                    label.style.paddingLeft = 8;
                    label.style.unityFontStyleAndWeight = data.IsTotal ? FontStyle.Bold : FontStyle.Normal;
                    label.text = FormatTimeSpan(data.Duration);
                    label.style.color = data.IsTotal
                        ? new StyleColor(StyleKeyword.Null)
                        : new StyleColor(BenchmarkCategoryTimeTracker.GetDurationColor(
                            data.Duration.TotalSeconds, minSeconds, maxSeconds));
                }
            });

            table.columns.Add(new Column
            {
                title = "Ratio (Total)",
                width = 150,
                makeCell = () => new ProgressBar(),
                bindCell = (element, i) =>
                {
                    var data = _results[i];
                    var progressBar = (ProgressBar) element;

                    var ratio = totalSeconds > 0d ? data.Duration.TotalSeconds / totalSeconds : 0d;
                    progressBar.value = (float) ratio * 100f;
                    progressBar.title = $"{ratio:P1}";

                    var progressFill = progressBar.Q(className: _progressUssClassName);
                    if (progressFill != null)
                    {
                        progressFill.style.backgroundColor = data.IsTotal
                            ? new StyleColor(StyleKeyword.Null)
                            : new StyleColor(BenchmarkCategoryTimeTracker.GetDurationColor(
                                data.Duration.TotalSeconds, minSeconds, maxSeconds));
                    }
                }
            });

            root.Add(table);
        }

        /// <summary>
        /// One row per <see cref="BenchmarkCategory"/> (ordered the same way as
        /// <see cref="BenchmarkRunner"/>'s console log breakdown) plus a trailing "Total" row, sourced from
        /// <see cref="BenchmarkCategoryTimeTracker"/>'s running totals for the last (or currently in-progress)
        /// benchmark run.
        /// </summary>
        private static List<BenchmarkCategoryResultData> BuildResultData()
        {
            var totals = BenchmarkCategoryTimeTracker.GetAllTotals();

            var results = totals
                .OrderBy(pair => pair.Key.ToString())
                .Select(pair => new BenchmarkCategoryResultData(pair.Key.ToString(), pair.Value, isTotal: false))
                .ToList();

            var totalDuration = BenchmarkCategoryTimeTracker.GetTotalDurationFromAllCategories();
            results.Add(new BenchmarkCategoryResultData("Total", totalDuration, isTotal: true));

            return results;
        }

        private static string FormatTimeSpan(TimeSpan duration)
        {
            return $"{duration:hh\\:mm\\:ss\\.fff}";
        }
    }
}
