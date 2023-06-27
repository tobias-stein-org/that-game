using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace tg.ui.view.debug
{
    using tg.debug.entities;
    using static PlasticPipe.PlasticProtocol.Messages.Serialization.ItemHandlerMessagesSerialization;

    public class ConsoleController : IViewController
    {
        private bool initialized    = false;

        public void activated(VisualElement view)
        {
            var input               = view.Q<TextField>("input");
            var history             = view.Q<ListView>("history");
            var scroll              = history.Q<ScrollView>();

            if(!this.initialized)
            {
                history.makeItem        = () => new Label();
                history.bindItem        = (ve, i) =>
                {
                    var data            = (Tuple<string, Color>)history.itemsSource[i];
                    var label           = ve as Label;
                    label.focusable     = false;
                    label.text          = data.Item1;
                    label.style.color   = data.Item2 * 0.8f;
                };

                history.fixedItemHeight = 20;
                history.itemsSource     = new List<Tuple<string, Color>>();

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
                            defer(() => history.ScrollToItem(-1));
                        }

                        input.SetValueWithoutNotify("|");
                        defer(input.Focus);
                    }
                });

                this.initialized        = true;
            }

            defer(input.Focus);
        }

        private void append(ListView history, Tuple<string, Color> elem)
        {
            history.itemsSource.Add(elem);
            if(history.itemsSource.Count > 5000)
            {
                history.itemsSource.RemoveAt(0);
            }
        }

        private void defer(System.Action action)
        {
            IEnumerator defer()
            {
                yield return new UnityEngine.WaitForEndOfFrame();
                action();
            }

            GameObject.Find("UI").GetComponent<UIDocument>().StartCoroutine(defer());
        }

        public void deactivated()
        {
        }
    }
}
