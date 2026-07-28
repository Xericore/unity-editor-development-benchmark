using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking
{
    /// <summary>
    /// Companion window to <see cref="UserWaitTimeEditorWindow"/>: lets you kick off (and, while one is running,
    /// stop) <see cref="BenchmarkRunner"/> from a single button instead of the menu/command line, and shows the
    /// same kind of category/progress-bar table, but for the last (or currently in-progress) automated benchmark
    /// run rather than live accumulated wait time. Rows are color-coded the same way as
    /// <see cref="BenchmarkRunner"/>'s console log breakdown (green shortest, red longest, relative to the other
    /// categories in that run), and a bold "Total" row shows the overall benchmark duration.
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

            var benchmarkButton = new Button
            {
                name = isRunning ? "stopBenchmarkButton" : "startBenchmarkButton",
                text = isRunning ? "Stop Benchmark" : "Start Benchmark",
                style = {maxWidth = new StyleLength(200)}
            };

            if (isRunning)
            {
                benchmarkButton.clicked += () =>
                {
                    BenchmarkRunner.StopBenchmark();
                    RefreshAndDrawData(root);
                };
            }
            else
            {
                benchmarkButton.clicked += () =>
                {
                    BenchmarkRunner.StartBenchmark();
                    RefreshAndDrawData(root);
                };
            }

            buttonRow.Add(benchmarkButton);

            root.Add(buttonRow);

            CreateTable(root);
        }

        private void CreateTable(VisualElement root)
        {
            var categoryResults = _results.Where(result => !result.IsTotal).ToList();

            // Color-coded (and ranged) by average duration, since that's what the Duration column displays; the
            // Ratio column separately uses each row's TotalDuration, so a category's ratio still reflects how
            // much of the whole run it actually consumed regardless of how many times it ran.
            var minSeconds = categoryResults.Count > 0 ? categoryResults.Min(result => result.AverageDuration.TotalSeconds) : 0d;
            var maxSeconds = categoryResults.Count > 0 ? categoryResults.Max(result => result.AverageDuration.TotalSeconds) : 0d;
            var totalSeconds = _results.Last().TotalDuration.TotalSeconds;

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
                title = "Avg. Duration",
                width = 130,
                makeCell = () => new Label(),
                bindCell = (element, i) =>
                {
                    var data = _results[i];
                    var label = (Label) element;
                    label.style.paddingLeft = 8;
                    label.style.unityFontStyleAndWeight = data.IsTotal ? FontStyle.Bold : FontStyle.Normal;
                    label.text = FormatTimeSpan(data.AverageDuration);
                    label.style.color = data.IsTotal
                        ? new StyleColor(StyleKeyword.Null)
                        : new StyleColor(BenchmarkCategoryTimeTracker.GetDurationColor(
                            data.AverageDuration.TotalSeconds, minSeconds, maxSeconds));
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

                    var ratio = totalSeconds > 0d ? data.TotalDuration.TotalSeconds / totalSeconds : 0d;
                    progressBar.value = (float) ratio * 100f;
                    progressBar.title = $"{ratio:P1}";

                    var progressFill = progressBar.Q(className: _progressUssClassName);
                    if (progressFill != null)
                    {
                        progressFill.style.backgroundColor = data.IsTotal
                            ? new StyleColor(StyleKeyword.Null)
                            : new StyleColor(BenchmarkCategoryTimeTracker.GetDurationColor(
                                data.AverageDuration.TotalSeconds, minSeconds, maxSeconds));
                    }
                }
            });

            root.Add(table);
        }

        /// <summary>
        /// One row per <see cref="BenchmarkCategory"/> (ordered the same way as
        /// <see cref="BenchmarkRunner"/>'s console log breakdown) plus a trailing "Total" row, sourced from
        /// <see cref="BenchmarkCategoryTimeTracker"/>'s running totals/averages for the last (or currently
        /// in-progress) benchmark run.
        /// </summary>
        private static List<BenchmarkCategoryResultData> BuildResultData()
        {
            var results = Enum.GetValues(typeof(BenchmarkCategory))
                .Cast<BenchmarkCategory>()
                .OrderBy(category => category.ToString())
                .Select(category => new BenchmarkCategoryResultData(
                    category.ToString(),
                    BenchmarkCategoryTimeTracker.GetAverage(category),
                    BenchmarkCategoryTimeTracker.GetTotal(category),
                    isTotal: false))
                .ToList();

            var totalDuration = BenchmarkCategoryTimeTracker.GetTotalDurationFromAllCategories();
            results.Add(new BenchmarkCategoryResultData("Total", totalDuration, totalDuration, isTotal: true));

            return results;
        }

        private static string FormatTimeSpan(TimeSpan duration)
        {
            return $"{duration:hh\\:mm\\:ss\\.fff}";
        }
    }
}
