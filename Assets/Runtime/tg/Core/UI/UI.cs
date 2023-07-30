using System;
using System.Linq;

using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;

using Unity.Entities;
using Unity.Collections;

namespace tg.ui
{
    using tg.events;
    using tg.application.events;
    using tg.ui.view;
    using tg.ui.events;

    namespace entities
    {
        using tg.application.entities;

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

        public struct UIViewRequested : IComponentData
        {
            public bool                 show;
        }

        public struct UIViewSpawned : IComponentData
        {
            public FixedString64Bytes   viewName;
        }

        public struct UIViewShow : IComponentData, IEnableableComponent {}
        public struct UIViewActive : IComponentData, IEnableableComponent {}
        public struct UIViewMenu : IComponentData {}

        public struct UIViewFade : IComponentData, IEnableableComponent
        {
            public enum Direction { In, Out }

            public float            fade;
            public float            time;
            public Direction        direction;
        }

        [UpdateInGroup(typeof(PresentationSystemGroup))]
        public partial class UISystemGroup : ComponentSystemGroup
        {
            internal const uint UPDATE_RATE_MS = (uint)(1.0f / 60.0f * 1000.0f);

            protected override void OnCreate()
            {
                base.OnCreate();
                this.RateManager = new RateUtils.VariableRateManager(UISystemGroup.UPDATE_RATE_MS, true);
            }
        }

        [CreateAfter(typeof(EventQueue))]
        [UpdateInGroup(typeof(UISystemGroup), OrderFirst = true)]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInitializing | ApplicationStateMask.AllowRunWhenMenuOpen | ApplicationStateMask.AllowRunWhenLoading | ApplicationStateMask.AllowRunWhenInGame, false)]
        public partial class UI : SystemBase, IEventListener
        {
            private const float                                 viewFadeTime = 0.2f;

            private UIDocument                                  ui = null;

            private NativeHashMap<FixedString64Bytes, Entity>   views;

            private EntityQuery                                 spawnViews;
            private EntityQuery                                 fadeViews;
            private EntityQuery                                 showViews;
            private EntityQuery                                 hideViews;
            private EntityQuery                                 activateViews;
            private EntityQuery                                 deactivateViews;

            protected override void OnCreate()
            {
                this.createUI();

                this.spawnViews         = this.GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(UIViewData), typeof(UIViewRequested) } });
                this.fadeViews          = this.GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(UIView), typeof(UIViewFade) } });

                this.showViews          = this.GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(UIView), typeof(UIViewShow) } });
                this.showViews.SetChangedVersionFilter(typeof(UIViewShow));

                this.hideViews          = this.GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(UIView) }, Disabled = new ComponentType[] { typeof(UIViewShow) } });
                this.hideViews.SetChangedVersionFilter(typeof(UIViewShow));

                this.activateViews      = this.GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(UIController), typeof(UIViewActive) } });
                this.activateViews.SetChangedVersionFilter(typeof(UIViewActive));

                this.deactivateViews    = this.GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(UIController) }, Disabled = new ComponentType[] { typeof(UIViewActive) } });
                this.deactivateViews.SetChangedVersionFilter(typeof(UIViewActive));

                this.RequireAnyForUpdate(new EntityQuery[] {
                    //StateManager.state(this),
                    this.spawnViews,
                    this.fadeViews,
                    this.showViews,
                    this.hideViews,
                    this.activateViews,
                    this.deactivateViews,
                });

                this.views  = new NativeHashMap<FixedString64Bytes, Entity>(16, Allocator.Persistent);

                EventQueue.subscribe(this);      
            }


            private void createUI()
            {
                var uiGO = new GameObject("UI");
                {
                    uiGO.AddComponent<EventSystem>();

                    this.ui = uiGO.AddComponent<UIDocument>();
                    {
                        var view            = ui.rootVisualElement;
                        view.style.top      = view.style.left   = 0;
                        view.style.width    = view.style.height = Length.Percent(100.0f);
                    }
                }
            }

            protected override void OnDestroy()
            {
                if(this.views.IsCreated) { this.views.Dispose(); }
            }

            protected override void OnUpdate()
            {
                // 2-step UI view spawning
                {
                    using(var ECB = new EntityCommandBuffer(Allocator.Temp, PlaybackPolicy.SinglePlayback))
                    {
                        foreach(var (data, requested, entity) in SystemAPI.Query<UIViewData, UIViewRequested>().WithEntityAccess())
                        {
                            this.spawnView(ECB, data, requested.show);
                            ECB.RemoveComponent<UIViewRequested>(entity);
                        }

                        if(!ECB.IsEmpty) { ECB.Playback(this.EntityManager); }
                    }

                    using(var ECB = new EntityCommandBuffer(Allocator.Temp, PlaybackPolicy.SinglePlayback))
                    {
                        foreach(var (spawned, entity) in SystemAPI.Query<UIViewSpawned>().WithEntityAccess())
                        {
                            this.views.Add(spawned.viewName, entity);
                            ECB.RemoveComponent<UIViewSpawned>(entity);
                        }

                        if(!ECB.IsEmpty) { ECB.Playback(this.EntityManager); }
                    }
                }

                // note: we have to process fading before checking de/active hide/show queries, since fade might change the state of
                // these queries. Since these queries are based on the "changed" filter, those changes wouldn't trigger these queries anymore.
                if(!this.fadeViews.IsEmpty)
                {
                    var entities    = this.fadeViews.ToEntityArray(Allocator.Temp);
                    var views       = this.fadeViews.ToComponentArray<UIView>();
                    var fades       = this.fadeViews.ToComponentDataArray<UIViewFade>(Allocator.Temp);

                    for(int i = 0; i < entities.Length; i++)
                    {
                        var entity  = entities[i];
                        var view    = views[i];
                        var fade    = fades[i];

                        fade.time  += (float)SystemAPI.Time.DeltaTime;

                        var t       = fade.time / fade.fade;

                        if(t >= 1.0f)
                        {
                            this.EntityManager.SetComponentEnabled<UIViewFade>(entity, false);

                            switch(fade.direction)
                            {
                                case UIViewFade.Direction.In:
                                {
                                    if(!this.EntityManager.HasComponent<UIViewActive>(entity))
                                    {
                                        this.EntityManager.AddComponent<UIViewActive>(entity);
                                    }
                                    this.EntityManager.SetComponentEnabled<UIViewActive>(entity, true);
                                    break;
                                }
                                case UIViewFade.Direction.Out:
                                {
                                    view.view.visible = false;
                                    view.view.style.display = DisplayStyle.None;
                                    this.EntityManager.SetComponentEnabled<UIViewActive>(entity, false);
                                    break;
                                }
                            }
                            continue;
                        }

                        view.view.style.opacity = Mathf.Clamp01(fade.direction == UIViewFade.Direction.In ? t : 1.0f - t);
                        this.EntityManager.SetComponentData<UIViewFade>(entity, fade);
                    }

                    entities.Dispose();
                    fades.Dispose();
                }

                if(!this.showViews.IsEmpty)
                {
                    var elem = this.showViews.ToComponentArray<UIView>();
                    var enti = this.showViews.ToEntityArray(Allocator.Temp);

                    for(int i = 0; i < elem.Length; i++)
                    {
                        if(this.EntityManager.HasComponent<UIViewFade>(enti[i]))
                        {
                            this.EntityManager.SetComponentData(enti[i], new UIViewFade
                            {
                                direction = UIViewFade.Direction.In,
                                fade = viewFadeTime,
                                time = 0.0f
                            });
                            this.EntityManager.SetComponentEnabled<UIViewFade>(enti[i], true);
                        }
                        else
                        {
                            if(!this.EntityManager.HasComponent<UIViewActive>(enti[i]))
                            {
                                this.EntityManager.AddComponent<UIViewActive>(enti[i]);
                            }
                            this.EntityManager.SetComponentEnabled<UIViewActive>(enti[i], true);
                        }

                        elem[i].view.visible        = true;
                        elem[i].view.style.display  = DisplayStyle.Flex;
                    }

                    enti.Dispose();
                }

                if(!this.hideViews.IsEmpty)
                {
                    var elem = this.hideViews.ToComponentArray<UIView>();
                    var enti = this.hideViews.ToEntityArray(Allocator.Temp);

                    for(int i = 0; i < elem.Length; i++)
                    {
                        if(this.EntityManager.HasComponent<UIViewFade>(enti[i]))
                        {
                            this.EntityManager.SetComponentData(enti[i], new UIViewFade
                            {
                                direction = UIViewFade.Direction.Out,
                                fade = viewFadeTime,
                                time = 0.0f
                            });
                            this.EntityManager.SetComponentEnabled<UIViewFade>(enti[i], true);
                        }
                        else
                        {
                            elem[i].view.visible        = false;
                            elem[i].view.style.display  = DisplayStyle.None;
                        }
                    }

                    enti.Dispose();
                }

                if(!this.activateViews.IsEmpty)
                {
                    var entities = this.activateViews.ToEntityArray(Allocator.Temp);
                    
                    for(int i = 0; i < entities.Length; i++)
                    {
                        var view = this.EntityManager.GetComponentObject<UIView>(entities[i]).view;
                        var ctrl = this.EntityManager.GetComponentObject<UIController>(entities[i]).controller;

                        ctrl.activated(view);
                    }

                    entities.Dispose();
                }

                if(!this.deactivateViews.IsEmpty)
                {
                    var entities = this.deactivateViews.ToEntityArray(Allocator.Temp);
                    
                    for(int i = 0; i < entities.Length; i++)
                    {
                        var view = this.EntityManager.GetComponentObject<UIView>(entities[i]).view;
                        var ctrl = this.EntityManager.GetComponentObject<UIController>(entities[i]).controller;

                        ctrl.deactivated(view);
                    }

                    entities.Dispose();
                }
            }

            void requestView(string name, bool show)
            {
                const string prefix = "tg.ui.view.";
                var withPrefix      = $"{prefix}{name}";

                // sync
                var viewData = Addressables.LoadAssetAsync<View>(withPrefix).WaitForCompletion();
                if(World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(new ComponentType(typeof(UIViewData))).ToComponentArray<UIViewData>().Where(data => data.name == name).Count() > 0)
                {
                    UnityEngine.Debug.LogWarning($"View '{name}' spawn already requested.");
                    return;
                }

                var viewEntity = this.EntityManager.CreateEntity();
#if UNITY_EDITOR
                this.EntityManager.SetName(viewEntity, $"view-{name}-data");
#endif

                this.EntityManager.AddComponentData<UIViewData>(viewEntity, viewData);
                this.EntityManager.AddComponentData<UIViewRequested>(viewEntity, new UIViewRequested { show = show });

                // async
                //                Addressables.LoadAssetAsync<View>(withPrefix).Completed += operation =>
                //                {
                //                    if(operation.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                //                    {
                //                        Debug.LogError($"Failed to load requested view '{name}'.");
                //                        return;
                //                    }

                //                    if(World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(new ComponentType(typeof(UIViewData))).ToComponentArray<UIViewData>().Where(data => data.name == name).Count() > 0)
                //                    {
                //                        UnityEngine.Debug.LogWarning($"View '{name}' spawn already requested.");
                //                        return;
                //                    }

                //                    var viewEntity = this.EntityManager.CreateEntity();
                //#if UNITY_EDITOR
                //                    this.EntityManager.SetName(viewEntity, $"view-{name}-data");
                //#endif

                //                    this.EntityManager.AddComponentData<UIViewData>(viewEntity, operation.Result);
                //                    this.EntityManager.AddComponent<UIViewRequested>(viewEntity);
                //                };
            }

            void spawnView(EntityCommandBuffer ECB, UIViewData data, bool show)
            {
                if(this.views.ContainsKey(data.name))
                {
                    UnityEngine.Debug.LogWarning($"View '{data.name}' already spawned.");
                    return;
                }
           
                var TController             = Type.GetType(data.controller);
                var controller              = (IViewController)Activator.CreateInstance(TController);

                // create new runtime UI VisualElement
                VisualElement view          = data.view.CloneTree();
                { 
                    view.userData           = data;
                    view.name               = FixedStringMethods.ConvertToString(ref data.name);
                    view.pickingMode        = data.isMenu ? PickingMode.Position : PickingMode.Ignore;
                    view.style.position     = Position.Absolute;

                    if(data.matchViewport)
                    {
                        view.style.top      = view.style.left   = 0;
                        view.style.width    = view.style.height = Length.Percent(100.0f);
                    }

                    // add view based on sort index
                    {
                        // first add view to main ui document
                        this.ui.rootVisualElement.Insert(0, view);

                        if(!data.isMenu)
                        {
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

                    this.ui.rootVisualElement.MarkDirtyRepaint();

                    EventQueue.publish(new ViewSpawnEvent { view = view });
                }

                // create a new entity representing the VisualElement

                var entity = ECB.CreateEntity();
                {
#if UNITY_EDITOR
                    ECB.SetName(entity, $"view-{data.name}");
#endif
                    ECB.AddComponent(entity, new UIView { view = view });
                    ECB.AddComponent(entity, new UIController { controller = controller });

                    ECB.AddComponent<UIViewShow>(entity);
                    ECB.SetComponentEnabled<UIViewShow>(entity, show);

                    if(data.fade)
                    {
                        ECB.AddComponent<UIViewFade>(entity);
                        ECB.SetComponent(entity, new UIViewFade
                        {
                            direction   = UIViewFade.Direction.In,
                            fade        = viewFadeTime,
                            time        = 0.0f
                        });
                    }

                    if(data.isMenu)
                    {
                        ECB.AddComponent<UIViewMenu>(entity);
                    }

                    ECB.AddComponent<UIViewSpawned>(entity, new UIViewSpawned { viewName = new FixedString64Bytes(data.name) });
                }
            }

            void onApplicationInitializedEvent(ApplicationInitializedEvent e)
            {
                var appData                     = e.data;
                var uiGO                        = GameObject.Find("UI");
                {
                    var uiInput = uiGO.AddComponent<InputSystemUIInputModule>();
                    {
                        var inputActions = appData.inputActions;
                        var inputUI = inputActions.FindActionMap("UI");

                        uiInput.actionsAsset = inputActions;

                        uiInput.leftClick = InputActionReference.Create(inputUI.FindAction("click"));
                        uiInput.point = InputActionReference.Create(inputUI.FindAction("point"));
                        uiInput.scrollWheel = InputActionReference.Create(inputUI.FindAction("scroll"));
                        uiInput.move = InputActionReference.Create(inputUI.FindAction("move"));
                        uiInput.submit = InputActionReference.Create(inputUI.FindAction("submit"));
                        uiInput.cancel = InputActionReference.Create(inputUI.FindAction("cancel"));
                    }

                    var ps = appData.uiSettings;
                    {
                        ps.name = "tg";

#if UNITY_STANDALONE
                        ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                        ps.referenceResolution = new Vector2Int { x = 1920, y = 1080 };
                        ps.match = 1.0f;
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

                    this.ui.panelSettings = ps;
                }
            }

            void onSpawnViewEvent(SpawnViewEvent e)
            {
                if(!this.views.TryGetValue(e.name, out Entity view))
                {
                    this.requestView(e.name, false);
                }
            }

            void onShowViewEvent(ShowViewEvent e)
            {
                if(this.views.TryGetValue(e.name, out Entity view))
                {
                    if(!this.EntityManager.IsComponentEnabled<UIViewShow>(view))
                    {
                        this.EntityManager.SetComponentEnabled<UIViewShow>(view, true);
                    }
                }
                // if view isn't spawned yet, request to spawn it
                else
                {
                    this.requestView(e.name, true);
                }
            }

            void onHideViewEvent(HideViewEvent e)
            {
                if(this.views.TryGetValue(e.name, out Entity view))
                {
                    if(this.EntityManager.IsComponentEnabled<UIViewShow>(view))
                    {
                        this.EntityManager.SetComponentEnabled<UIViewShow>(view, false);
                    }
                }
            }

            void onToggleViewEvent(ToggleViewEvent e)
            {
                if(this.views.TryGetValue(e.name, out Entity view))
                {
                    var state           = this.EntityManager.IsComponentEnabled<UIViewShow>(view) ? false : true;
                    this.EntityManager.SetComponentEnabled<UIViewShow>(view, state);
                }
                else
                {
                    this.requestView(e.name, true);
                }
            }
        }
    }
}
