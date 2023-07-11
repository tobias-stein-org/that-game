using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace tg.editor.ai.behaviour.tree
{
    using System.Linq;
    using tg.ai.behaviour.tree;

    public class BlackboardViewController
    {

        private MultiColumnListView             view;
        private BehaviourTree                   tree;

        private Dictionary<Tuple<string, Type>, List<Tuple<Node, FieldInfo>>> blackboardValueFields = new Dictionary<Tuple<string, Type>, List<Tuple<Node, FieldInfo>>>();

        public BlackboardViewController(MultiColumnListView view)
        {
            this.view                               = view;

            this.view.columns["key"].makeCell       = () => new Label();
            this.view.columns["key"].bindCell       = (VisualElement e, int i) =>
            {
                var label                           = e as Label;

                var bbvKey                          = this.view.itemsSource[i] as string;
                var keys                            = this.blackboardValueFields.Keys.Where(key => key.Item1 == bbvKey).ToArray();

                label.text                          = bbvKey;
                if(keys.Length > 1)
                {
                    label.style.color               = Color.red;
                    label.tooltip                   = $"There are multiple blackboard values with the same key, but different value type.";
                }
            };

            this.view.columns["type"].makeCell      = () => new Label();
            this.view.columns["type"].bindCell      = (VisualElement e, int i) =>
            {
                var label                           = e as Label;
                var bbvKey                          = this.view.itemsSource[i] as string;
                var keys                            = this.blackboardValueFields.Keys.Where(key => key.Item1 == bbvKey);

                label.text                          = string.Join(",", keys.Select(key => key.Item2.Name));
                label.tooltip                       = string.Join(",", keys.Select(key => key.Item2.FullName));
            };

            this.view.columns["used_by"].makeCell   = () => new DropdownField();
            this.view.columns["used_by"].bindCell   = (VisualElement e, int i) =>
            {
                var dropdown                        = e as DropdownField;
                var bbvKey                          = this.view.itemsSource[i] as string;
                var nodes                           = this.blackboardValueFields.Keys.Where(key => key.Item1 == bbvKey).SelectMany(key => this.blackboardValueFields[key].Select(value => value.Item1) );

                dropdown.choices                    = nodes.Select(node => $"{node.name} ({node.id})").ToList();
                dropdown.index                      = 0;
            };
        }

        public void build(BehaviourTree tree)
        {
            this.blackboardValueFields.Clear();
            this.view.Clear();

            if(tree && (Application.isPlaying || AssetDatabase.CanOpenAssetInEditor(tree.GetInstanceID())))
            {
                this.tree = tree;
                this.refresh();
            }
        }

        private static readonly Type TBlackboardValue = typeof(BlackboardValue<>);

        public void refresh()
        {
            this.blackboardValueFields.Clear();

            List<FieldInfo> getBlackboardValueFields(Node node)
            {
                return node
                    .GetType()
                    .GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(field => field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == TBlackboardValue).ToList();
            }

            foreach(var node in this.tree.nodes)
            { 
                foreach(var field in getBlackboardValueFields(node))
                {
                    var bbv         = field.GetValue(node);

                    var bbvType     = field.FieldType;
                    var bbvGenType  = bbvType.GenericTypeArguments[0];

                    var keyField    = bbvType.GetField("key", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var keyValue    = keyField.GetValue(bbv) as string;
                    if(string.IsNullOrWhiteSpace(keyValue))
                    {
                        // if key not set yet, use key specified by BlackboardValueAttribute or fallback to field name
                        var attr = field.GetCustomAttribute<tg.ai.behaviour.tree.BlackboardValueAttribute>();
                        keyValue = attr != null ? attr.name : field.Name;
                    }

                    var key = new Tuple<string, Type>(keyValue, bbvGenType);
                    if(!this.blackboardValueFields.TryGetValue(key, out List<Tuple<Node, FieldInfo>> value))
                    {
                        value = new List<Tuple<Node, FieldInfo>>();
                    }

                    value.Add(new Tuple<Node, FieldInfo>(node, field));
                    this.blackboardValueFields[key] = value;
                }
            }

            this.view.itemsSource = this.blackboardValueFields.Keys.Select(key => key.Item1).ToList();
            this.view.Rebuild();
        }
    }
}
