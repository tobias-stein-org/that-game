using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.UIElements;

namespace tg.ui.view
{
    using tg.debug.entities;

    public class consoleController : IViewController
    {
        private bool initialized = false;

        public void activated(VisualElement view)
        {
            var input = view.Q<TextField>("input");
            var history = view.Q<ListView>("history");
            var scroll = history.Q<ScrollView>();

            if(!this.initialized)
            {
                history.makeItem = () => new Label();
                history.bindItem = (ve, i) =>
                {
                    var data = (Tuple<string, Color>)history.itemsSource[i];
                    var label = ve as Label;
                    label.focusable = false;
                    label.text = data.Item1;
                    label.style.color = data.Item2 * 0.8f;
                };

                history.fixedItemHeight = 20;
                history.itemsSource = new List<Tuple<string, Color>>();

                input.RegisterValueChangedCallback((ChangeEvent<string> e) =>
                {
                    input.SetValueWithoutNotify($"{string.Concat(e.newValue.Split('|').Where(v => !string.IsNullOrEmpty(v)))}|");
                });

                input.RegisterCallback((KeyDownEvent e) =>
                {
                    if(e.keyCode == KeyCode.Return && input.value.Length > 1)
                    {
                        var cmd = input.value.Substring(0, input.value.Length - 1).Trim();
                        if(!string.IsNullOrEmpty(cmd))
                        {
                            if(cmd.Equals("/clear"))
                            {
                                history.itemsSource.Clear();
                            }
                            else
                            {
                                var result = Console.invokeCommand(cmd);

                                if(result.hasError && result.result.Length > 0)
                                {
                                    this.append(history, new Tuple<string, Color>(cmd, Color.red));
                                }
                                else
                                {
                                    this.append(history, new Tuple<string, Color>(cmd, Color.white));
                                }

                                if(result.result.Length > 0)
                                {
                                    foreach(var str in result.result)
                                    {
                                        this.append(history, new Tuple<string, Color>(str, result.hasError ? Color.red : Color.white));
                                    }
                                }
                            }

                            history.RefreshItems();

                            view.schedule.Execute(() =>
                            {
                                history.ScrollToItem(-1);
                                input.SetValueWithoutNotify("|");
                                input.Focus();
                            }).ExecuteLater(50);
                        }
                    }
                });

                this.initialized = true;
            }

            view.schedule.Execute(() => input.Focus()).ExecuteLater(50);
        }

        private void append(ListView history, Tuple<string, Color> elem)
        {
            history.itemsSource.Add(elem);
            if(history.itemsSource.Count > 5000)
            {
                history.itemsSource.RemoveAt(0);
            }
        }

        public void deactivated()
        {
        }
    }
}