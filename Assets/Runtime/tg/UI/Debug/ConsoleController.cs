using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace tg.ui.view.debug
{
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
                    var label           = ve as Label;
                    label.focusable     = false;
                    label.text          = (string)history.itemsSource[i];
                    label.style.color   = Color.white * 0.8f;
                };

                history.fixedItemHeight = 20;
                history.itemsSource     = new List<string>();

                input.delegatesFocus    = false;

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
                            // TODO: process command

                            history.itemsSource.Add(cmd);
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
