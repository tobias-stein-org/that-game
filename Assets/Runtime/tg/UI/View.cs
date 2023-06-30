using UnityEngine;
using UnityEngine.UIElements;

using Unity.Entities;

namespace tg.ui
{
    using tg.ui.view;

    public class View : ScriptableObject
    {
        public const string             label           = "view";

        [HideInInspector]
        public new string               name            = System.Guid.NewGuid().ToString().Split('-')[0];

        [HideInInspector]
        public VisualTreeAsset          view;

        [HideInInspector]
        public string                   controller;

        public bool                     show            = true;

        public bool                     matchViewport   = true;

        public bool                     isMenu          = false;

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
                Show                        = 1 << 0,
                MatchViewport               = 1 << 1,
                IsMenu                      = 1 << 2,
            }

            public string               name;

            public VisualTreeAsset      view;

            public string               controller;

            public ViewProperties       properties;


            private bool                get(ViewProperties property) { return (this.properties & property) != 0; }
            private void                set(ViewProperties property) { this.properties |= property; }
            private void                clr(ViewProperties property) { this.properties &= ~property; }

            public bool                 show
            {
                get { return get(ViewProperties.Show); }
                set { if(value) { set(ViewProperties.Show); } else { clr(ViewProperties.Show); } }
            }

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

            public int                  sort;

        }

        //public class ViewAuthoring : Baker<View>
        //{
        //    public override void Bake(View authoring)
        //    {
        //        DependsOn(authoring.view);
        //        DependsOn(authoring.controller);

        //        if(authoring.view == null) { return; }
        //        if(authoring.controller == null) { return; }
        //        if(string.IsNullOrEmpty(authoring.name)) { return; }

        //        var viewController  = GetEntity(TransformUsageFlags.None);
                
        //        AddComponentObject<UIViewData>(viewController, new UIViewData
        //        {
        //            name                = authoring.name,
        //            view                = authoring.view,
        //            controller          = authoring.controller,
        //            show                = authoring.show,
        //            matchViewport       = authoring.matchViewport,
        //            isMenu              = authoring.isMenu,
        //            sort                = authoring.sort
        //        });
        //    }
        //}
    }
}
