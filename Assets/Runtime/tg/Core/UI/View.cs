using UnityEngine;
using UnityEngine.UIElements;

using Unity.Entities;
using Unity.Collections;

namespace tg.ui
{
    public class View : ScriptableObject
    {
        public const string             label           = "view";

        [HideInInspector]
        public new string               name            = System.Guid.NewGuid().ToString().Split('-')[0];

        [HideInInspector]
        public VisualTreeAsset          view;

        [HideInInspector]
        public string                   controller;

        public bool                     matchViewport   = true;

        public bool                     isMenu          = false;

        public bool                     fade            = true;

        public int                      sort            = 0;
    }

    namespace entities
    {
        public class UIViewData : IComponentData
        {
            [System.Flags]
            public enum ViewProperties
            {
                None                        = 0,
                MatchViewport               = 1,
                IsMenu                      = 1 << 1,
                Fade                        = 1 << 2,
            }

            public FixedString64Bytes   name;

            public VisualTreeAsset      view;

            public string               controller;

            public ViewProperties       properties;


            private bool                get(ViewProperties property) { return (this.properties & property) != 0; }
            private void                set(ViewProperties property) { this.properties |= property; }
            private void                clr(ViewProperties property) { this.properties &= ~property; }

            public bool                 matchViewport
            {
                get { return get(ViewProperties.MatchViewport); }
                set { if(value) { set(ViewProperties.MatchViewport); } else { clr(ViewProperties.MatchViewport); } }
            }

            public bool                 isMenu
            {
                get { return get(ViewProperties.IsMenu); }
                set { if(value) { set(ViewProperties.IsMenu); } else { clr(ViewProperties.IsMenu); } }
            }

            public bool                 fade
            {
                get { return get(ViewProperties.Fade); }
                set { if(value) { set(ViewProperties.Fade); } else { clr(ViewProperties.Fade); } }
            }

            public int                  sort;

            public static implicit operator UIViewData(View view)
            {
                return new UIViewData
                {
                    name            = view.name,
                    view            = view.view,
                    controller      = view.controller,
                    matchViewport   = view.matchViewport,
                    isMenu          = view.isMenu,
                    sort            = view.sort,
                    fade            = view.fade,
                };
            }
        }
    }
}
