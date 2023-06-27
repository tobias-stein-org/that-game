using System;
using System.Reflection;
using System.Linq;

using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;

using Unity.Entities;
using Unity.Collections;

namespace tg.ui
{
    using tg.application;
    using tg.events;
    using tg.application.events;
    using tg.ui.view;
    using tg.ui.events;
    using Unity.Entities.UniversalDelegates;

    namespace entities
    {
        public class UIDocumentData : IComponentData
        {
            public UIDocument           ui;
        }

        public class UIView : IComponentData
        {
            public VisualElement        view;
        }

        public class UIController : IComponentData
        {
            public view.IViewController controller;
        }

        public struct UIViewShow : IComponentData, IEnableableComponent {}
        public struct UIViewActive : IComponentData, IEnableableComponent {}
        public struct UIViewMenu : IComponentData {}

        public struct UIViewFade : IComponentData, IEnableableComponent
        {
            public enum Direction { In, Out }

            public float            fade;
            public float            t;
            public Direction        direction;
        }

        [UpdateInGroup(typeof(PresentationSystemGroup))]
        public partial class UISystemGroup : ComponentSystemGroup
        {

        }

        [CreateAfter(typeof(EventQueue))]
        [UpdateInGroup(typeof(UISystemGroup), OrderFirst = true)]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenMenuOpen | ApplicationStateMask.AllowRunWhenLoading | ApplicationStateMask.AllowRunWhenInGame, false)]
        public partial struct UI : ISystem, IEventListener<UI>
        {
            private NativeHashMap<FixedString64Bytes, Entity>   views;

            private EntityQuery manageViewData;
            private EntityQuery showViews;
            private EntityQuery hideViews;
            private EntityQuery activateViews;
            private EntityQuery deactivateViews;

            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate(StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<UI>(state.SystemHandle)));

                this.manageViewData = state.GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(UIViewData) } });
                this.manageViewData.SetChangedVersionFilter(typeof(UIViewData));

                this.showViews = state.GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(UIView), typeof(UIViewShow) }});
                this.showViews.SetChangedVersionFilter(typeof(UIViewShow));

                this.hideViews = state.GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(UIView) }, Disabled = new ComponentType[] { typeof(UIViewShow) } });
                this.hideViews.SetChangedVersionFilter(typeof(UIViewShow));

                this.activateViews = state.GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(UIController), typeof(UIViewActive) } });
                this.activateViews.SetChangedVersionFilter(typeof(UIViewActive));

                this.deactivateViews = state.GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(UIController) }, Disabled = new ComponentType[] { typeof(UIViewActive) } });
                this.deactivateViews.SetChangedVersionFilter(typeof(UIViewActive));

                state.RequireAnyForUpdate(new EntityQuery[] {
                    this.manageViewData,
                    this.showViews,
                    this.hideViews,
                    this.activateViews,
                    this.deactivateViews
                });

                this.views  = new NativeHashMap<FixedString64Bytes, Entity>(16, Allocator.Persistent);

                EventQueue.subscribe(state.WorldUnmanaged.GetUnsafeSystemRef<UI>(state.SystemHandle));      
            }

            public void OnDestroy(ref SystemState state)
            {
                if(this.views.IsCreated) { this.views.Dispose(); }
            }

            public void OnUpdate(ref SystemState state)
            {
                if(!this.manageViewData.IsEmpty)
                {
                    var ui  = SystemAPI.ManagedAPI.GetSingleton<UIDocumentData>().ui;

                    foreach(var data in this.manageViewData.ToComponentArray<UIViewData>())
                    {
                        this.spawnView(ref state, data, ui);
                    }
                }

                if(!this.showViews.IsEmpty)
                {
                    var elem = this.showViews.ToComponentArray<UIView>();

                    for(int i = 0; i < elem.Length; i++)
                    {
                        elem[i].view.visible        = true;
                        elem[i].view.style.display  = DisplayStyle.Flex;
                    }
                }

                if(!this.hideViews.IsEmpty)
                {
                    var elem = this.hideViews.ToComponentArray<UIView>();

                    for(int i = 0; i < elem.Length; i++)
                    {
                        elem[i].view.visible        = false;
                        elem[i].view.style.display  = DisplayStyle.None;
                    }
                }

                if(!this.activateViews.IsEmpty)
                {
                    var entities = this.activateViews.ToEntityArray(Allocator.Temp);
                    
                    for(int i = 0; i < entities.Length; i++)
                    {
                        var view = state.EntityManager.GetComponentObject<UIView>(entities[i]).view;
                        var ctrl = state.EntityManager.GetComponentObject<UIController>(entities[i]).controller;

                        ctrl.activated(view);
                    }
                }

                if(!this.deactivateViews.IsEmpty)
                {
                    var entities = this.deactivateViews.ToEntityArray(Allocator.Temp);
                    
                    for(int i = 0; i < entities.Length; i++)
                    {
                        var view = state.EntityManager.GetComponentObject<UIView>(entities[i]).view;
                        var ctrl = state.EntityManager.GetComponentObject<UIController>(entities[i]).controller;

                        ctrl.deactivated();
                    }
                }
            }

            void spawnView(ref SystemState state, UIViewData data, UIDocument ui)
            {
                var TController             = Type.GetType(data.controller.assemblyReference);
                var controller              = (IViewController)Activator.CreateInstance(TController);

                // create new runtime UI VisualElement
                VisualElement view          = data.view.CloneTree();
                { 
                    view.userData           = data;
                    view.name               = data.name;
                    view.name               = data.name;
                    view.style.position     = Position.Absolute;

                    if(data.matchViewport)
                    {
                        view.style.top      = view.style.left   = 0;
                        view.style.width    = view.style.height = Length.Percent(100.0f);
                    }


                    // add view based on sort index
                    {
                        // first add view to main ui document
                        ui.rootVisualElement.Add(view);

                        // by default place in in the back first
                        view.SendToBack();

                        // then compare its sort order with any other view in the ui and place it in front of the forst view with a lower sort index
                        var views = ui.rootVisualElement.Children().ToList();

                        // note: we must sort views first according their order. using the child list directly does not seem to guarantee correct sort order (highest sort index first)
                        views.Sort((a, b) => (b.userData as UIViewData).sort - (a.userData as UIViewData).sort);

                        foreach(var other in views)
                        {
                            if(other != view && (view.userData as UIViewData).sort <= data.sort)
                            {
                                Debug.Log($"Place view {view.name} [sort: {data.sort}] in front of {other.name} [sort: {(other.userData as UIViewData).sort}]");
                                view.PlaceInFront(other);
                                break;
                            }
                        }
                    }
                }

                // create a new entity representing the VisualElement 
                var entity = state.EntityManager.CreateEntity();
                {
                    state.EntityManager.SetName(entity, $"view-{data.name}");
                    state.EntityManager.AddComponentObject(entity, new UIView { view = view });
                    state.EntityManager.AddComponentObject(entity, new UIController { controller = controller });
                    state.EntityManager.AddComponent<UIViewShow>(entity);
                    state.EntityManager.AddComponent<UIViewActive>(entity);

                    if(data.isMenu)
                    {
                        state.EntityManager.AddComponent<UIViewMenu>(entity);
                    }

                    if(!data.show)
                    {
                        state.EntityManager.SetComponentEnabled<UIViewShow>(entity, false);
                        state.EntityManager.SetComponentEnabled<UIViewActive>(entity, false);
                    }

                    this.views.Add(new FixedString64Bytes(data.name), entity);
                }
            }

            void onApplicationInitializedEvent(ApplicationInitializedEvent e)
            {
                var appData = e.appData;
                var uiGO    = new GameObject("UI");
                {
                    uiGO.AddComponent<EventSystem>();

                    var uiInput = uiGO.AddComponent<InputSystemUIInputModule>();
                    {
                        var inputActions        = appData.inputActions.result;
                        var inputUI             = inputActions.FindActionMap("UI");

                        uiInput.actionsAsset    = inputActions;

                        uiInput.leftClick       = InputActionReference.Create(inputUI.FindAction("click"));
                        uiInput.point           = InputActionReference.Create(inputUI.FindAction("point"));
                        uiInput.scrollWheel     = InputActionReference.Create(inputUI.FindAction("scroll"));
                        uiInput.move            = InputActionReference.Create(inputUI.FindAction("move"));
                        uiInput.submit          = InputActionReference.Create(inputUI.FindAction("submit"));
                        uiInput.cancel          = InputActionReference.Create(inputUI.FindAction("cancel"));
                    }

                    var ps  = ScriptableObject.CreateInstance<PanelSettings>();
                    {
                        ps.name                 = "tg";
                        ps.themeStyleSheet      = appData.uiTheme.result;

#if UNITY_STANDALONE
                        ps.screenMatchMode      = PanelScreenMatchMode.MatchWidthOrHeight;
                        ps.referenceResolution  = new Vector2Int { x = 1920, y = 1080 };
                        ps.match                = 0.0f;
#else
                        ps.screenMatchMode      = PanelScreenMatchMode.MatchWidthOrHeight;
                        // portait
                        //ps.referenceResolution  = new Vector2Int { x = 1125, y = 2436 };
                        //ps.match                = 1.0f;
                        // landscape
                        ps.match                = 0.0f;
                        ps.referenceResolution  = new Vector2Int(2436, 1125);
#endif
                    }

                    var ui  = uiGO.AddComponent<UIDocument>();
                    {
                        ui.panelSettings        = ps;

                        var view                = ui.rootVisualElement;
                        view.style.top          = view.style.left   = 0;
                        view.style.width        = view.style.height = Length.Percent(100.0f);
                    }

                    World.DefaultGameObjectInjectionWorld.EntityManager
                        .AddComponentObject(World.DefaultGameObjectInjectionWorld.Unmanaged.GetExistingUnmanagedSystem<UI>(), new UIDocumentData { ui = ui });
                }
            }

            void onShowViewEvent(ShowViewEvent e)
            {
                if(this.views.TryGetValue(e.name, out Entity view))
                {
                    var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                    if(!entityManager.IsComponentEnabled<UIViewShow>(view))
                    {
                        entityManager.SetComponentEnabled<UIViewShow>(view, true);
                        entityManager.SetComponentEnabled<UIViewActive>(view, true);
                    }

                    //if(e.activateController && !entityManager.IsComponentEnabled<UIViewActive>(view))
                    //{
                    //    entityManager.SetComponentEnabled<UIViewActive>(view, true);
                    //}
                }
            }

            void onHideViewEvent(HideViewEvent e)
            {
                if(this.views.TryGetValue(e.name, out Entity view))
                {
                    var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                    if(entityManager.IsComponentEnabled<UIViewShow>(view))
                    {
                        entityManager.SetComponentEnabled<UIViewShow>(view, false);
                        entityManager.SetComponentEnabled<UIViewActive>(view, false);
                    }

                    //if(e.deactivateController && entityManager.IsComponentEnabled<UIViewActive>(view))
                    //{
                    //    entityManager.SetComponentEnabled<UIViewActive>(view, false);
                    //}
                }
            }

            void onToggleViewEvent(ToggleViewEvent e)
            {
                if(this.views.TryGetValue(e.name, out Entity view))
                {
                    var entityManager   = World.DefaultGameObjectInjectionWorld.EntityManager;
                    var state           = entityManager.IsComponentEnabled<UIViewShow>(view) ? false : true;
                    entityManager.SetComponentEnabled<UIViewShow>(view, state);
                    entityManager.SetComponentEnabled<UIViewActive>(view, state);

                    //if(e.toggleController)
                    //{
                    //    entityManager.SetComponentEnabled<UIViewActive>(view, entityManager.IsComponentEnabled<UIViewActive>(view) ? false : true);
                    //}
                }
            }
        }
    }
}
