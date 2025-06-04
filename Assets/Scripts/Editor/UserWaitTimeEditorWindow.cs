using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    public class UserWaitTimeEditorWindow : EditorWindow
    {
        [MenuItem("Window/Analysis/User Wait Time Editor Window")]
        public static void ShowExample()
        {
            var wnd = GetWindow<UserWaitTimeEditorWindow>();
            wnd.titleContent = new GUIContent("User Wait Time Editor Window");
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            var root = rootVisualElement;

            // VisualElements objects can contain other VisualElement following a tree hierarchy
            var label = new Label("Hello World!");
            root.Add(label);

            var button = new Button
            {
                name = "button",
                text = "Button",
                style = {maxWidth = new StyleLength(160)}
            };
            root.Add(button);

            var toggle = new Toggle
            {
                name = "toggle",
                label = "Toggle"
            };
            root.Add(toggle);

            CreateTable(root);
        }

        private static void CreateTable(VisualElement root)
        {
            var people = new List<Person>
            {
                new() {Name = "Alice", Age = 30},
                new() {Name = "Bob", Age = 25},
                new() {Name = "Charlie", Age = 35}
            };

            var table = new MultiColumnListView
            {
                itemsSource = people,
                showBoundCollectionSize = false,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                style = {flexGrow = 1}
            };

            table.columns.Add(new Column
            {
                title = "Name",
                width = 120,
                makeCell = () => new Label(),
                bindCell = (element, i) => ((Label) element).text = people[i].Name
            });

            table.columns.Add(new Column
            {
                title = "Age",
                width = 60,
                makeCell = () => new Label(),
                bindCell = (element, i) => ((Label) element).text = people[i].Age.ToString()
            });

            root.Add(table);
        }
    }

    [Serializable]
    public class Person
    {
        public string Name;
        public int Age;
    }
}