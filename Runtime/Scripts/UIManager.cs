﻿using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TinaX;
using TinaX.Services;
using TinaX.UIKit.Internal;
using TinaX.UIKit.Const;
using UnityEngine.UI;
using TinaX.UIKit.Entity;
using TinaX.XComponent;
using UniRx;

namespace TinaX.UIKit
{
    public class UIManager : IUIKit, IUIKitInternal
    {
        [Inject(true)]
        [CatLib.Container.Inject(Required = false)]
        public IAssetService Assets { get; set; }

        private UIConfig mConfig;

        private UIGroup mCurUIGroup;
        private UINameMode mUINameMode;
        private string mUIRootDirLoadPath;
        private string mUIRootDirLoadPath_withSlash;

        private bool mInited = false;
        private XException mStartException;

        private GameObject mUIKit_Root_Go;
        private Canvas mScreenUIRoot_Canvas;
        private Camera mScreenUICamera;
        private Dictionary<int, GameObject> mDict_UIRoot_SortingLayer_Go = new Dictionary<int, GameObject>();
        private Dictionary<int, Canvas> mDict_UIRootCanvas_SortingLayer_Canvas = new Dictionary<int, Canvas>();
        /// <summary>
        /// Key: SortingLayer
        /// </summary>
        private Dictionary<int, UILayerManager> mDict_UILayers = new Dictionary<int, UILayerManager>();

        private UIEntityManager UIEntities = new UIEntityManager();

        public async Task<bool> Start()
        {
            #region config
            if (mInited) return true;
            mConfig = XConfig.GetConfig<UIConfig>(UIConst.ConfigPath_Resources);
            if (mConfig == null)
            {
                mStartException = new XException("[TinaX.UIKit] Connot found config file."); ;
                return false;
            }
            if (!mConfig.EnableUIKit) return true;

            if (mConfig.UINameMode == UINameMode.UIGroup)
                mCurUIGroup = mConfig.DefaultUIGroup;
            else
            {
                mUIRootDirLoadPath = mConfig.UIRootDirectoryLoadPath;
                if (!mUIRootDirLoadPath.IsNullOrEmpty())
                {
                    if (mUIRootDirLoadPath.EndsWith("/"))
                        mUIRootDirLoadPath = mUIRootDirLoadPath.Substring(0, mUIRootDirLoadPath.Length - 1);
                    mUIRootDirLoadPath_withSlash = mUIRootDirLoadPath + "/";
                }
            }
            mUINameMode = mConfig.UINameMode;

                #endregion


            //Init UIKit GameObjects
                #region UIKit GameObjects
            mUIKit_Root_Go = XCore.GetMainInstance().BaseGameObject
                .FindOrCreateGameObject("UIKit")
                .SetPosition(new Vector3(-9999, -9999, -9999));

            if (mConfig.UseUICamera)
            {
                var camera_config = mConfig.UICameraConfig;
                if (camera_config == null)
                    camera_config = new UICameraConfig();

                mScreenUICamera = mUIKit_Root_Go.FindOrCreateGameObject("UICamera")
                    .AddComponent<Camera>();
                mScreenUICamera.clearFlags = camera_config.clearFlags;
                mScreenUICamera.backgroundColor = camera_config.backgroundColor;
                mScreenUICamera.cullingMask = camera_config.cullingMask;
                mScreenUICamera.orthographic = camera_config.orthographic;
                mScreenUICamera.orthographicSize = camera_config.orthographicSize;
                mScreenUICamera.nearClipPlane = camera_config.nearClipPlane;
                mScreenUICamera.farClipPlane = camera_config.farClipPlane;
                mScreenUICamera.depth = camera_config.depth;
                mScreenUICamera.renderingPath = camera_config.renderingPath;
                mScreenUICamera.targetTexture = camera_config.targetTexture;
                mScreenUICamera.useOcclusionCulling = camera_config.useOcclusionCulling;
                mScreenUICamera.allowHDR = camera_config.allowHDR;
                mScreenUICamera.allowMSAA = camera_config.allowMSAA;

                
            }

            //Default UIRoot
            refreshUIRoot(0);

            //EventSystem
#if ENABLE_LEGACY_INPUT_MANAGER
            if (mConfig.AutoCreateEventSystem)
            {
                var es_go = GameObjectHelper.FindOrCreateGameObject("EventSystem");
                var event_system = es_go.GetComponentOrAdd<UnityEngine.EventSystems.EventSystem>();
                event_system.sendNavigationEvents = true;
                event_system.pixelDragThreshold = 10;
                var input_module = es_go.GetComponentOrAdd<UnityEngine.EventSystems.StandaloneInputModule>();
            }
#endif

            #endregion

            await Task.Delay(0);
            return true;
        }
        public XException GetStartException() => mStartException;

        #region OpenUI方法

        //------------UIName 和 参数 -----------------------------------------------------------

        public IUIEntity OpenUI(string UIName, params object[] args)
        {
            string ui_path = null;
            if (mUINameMode == UINameMode.UIGroup)
            {
                if (mCurUIGroup != null)
                {
                    if (!mCurUIGroup.TryGetPath(UIName, out ui_path))
                        throw new UIKitException("[TinaX.UIKit] Invalid UIName : " + UIName, UIKitErrorCode.InvalidUIName);
                }
            }
            else
                ui_path = (mUIRootDirLoadPath.IsNullOrEmpty()) ? UIName : mUIRootDirLoadPath_withSlash + UIName;

            if (ui_path.IsNullOrEmpty())
                throw new UIKitException("[TinaX.UIKit] " + (IsChinese ? $"未能获取到UI \"{UIName}\" 的加载路径，请检查设置或传入参数" : $"Cannot get UI Path by UI Name \"{UIName}\", Please check config or args."), UIKitErrorCode.ConnotGetUIPath);

            var entity = this.openUI(ui_path, UIName, null, null, false, false, default, args);
            return entity;
        }
        public async Task<IUIEntity> OpenUIAsync(string UIName, params object[] args)
        {
            string ui_path = null;
            if(mUINameMode == UINameMode.UIGroup)
            {
                if(mCurUIGroup != null)
                {
                    if (!mCurUIGroup.TryGetPath(UIName, out ui_path))
                        throw new UIKitException("[TinaX.UIKit] Invalid UIName : " + UIName, UIKitErrorCode.InvalidUIName);
                }
            }
            else
            {
                ui_path = (mUIRootDirLoadPath.IsNullOrEmpty()) ? UIName : mUIRootDirLoadPath_withSlash + UIName;
            }

            if (ui_path.IsNullOrEmpty())
            {
                throw new UIKitException("[TinaX.UIKit] " + (IsChinese ? $"未能获取到UI \"{UIName}\" 的加载路径，请检查设置或传入参数" : $"Cannot get UI Path by UI Name \"{UIName}\", Please check config or args."), UIKitErrorCode.ConnotGetUIPath);
            }

            var entity = await openUIAsync(ui_path, UIName, null, null, false, false, default, args);
            return entity;

        }
        public void OpenUIAsync(string UIName, Action<IUIEntity,XException> callback, params object[] args)
        {
            this.OpenUIAsync(UIName, args)
                .ToObservable()
                .ObserveOnMainThread()
                .Subscribe(entity =>
                {
                    callback?.Invoke(entity, null);
                },
                e => 
                {
                    if (e is XException)
                        callback?.Invoke(null, e as XException);
                    else
                        Debug.LogException(e);
                });
        }

        //-----------UIName 和 xBehaviour 和 参数 ---------------------------------------------
        public IUIEntity OpenUI<T>(string UIName, params object[] args) where T : XBehaviour
        {
            string ui_path = null;
            if (mUINameMode == UINameMode.UIGroup)
            {
                if (mCurUIGroup != null)
                {
                    if (!mCurUIGroup.TryGetPath(UIName, out ui_path))
                        throw new UIKitException("[TinaX.UIKit] Invalid UIName : " + UIName, UIKitErrorCode.InvalidUIName);
                }
            }
            else
                ui_path = (mUIRootDirLoadPath.IsNullOrEmpty()) ? UIName : mUIRootDirLoadPath_withSlash + UIName;

            if (ui_path.IsNullOrEmpty())
                throw new UIKitException("[TinaX.UIKit] " + (IsChinese ? $"未能获取到UI \"{UIName}\" 的加载路径，请检查设置或传入参数" : $"Cannot get UI Path by UI Name \"{UIName}\", Please check config or args."), UIKitErrorCode.ConnotGetUIPath);

            var xbehaviour = (XBehaviour)XCore.GetMainInstance().CreateInstance(typeof(T));
            return this.openUI(ui_path, UIName, null, xbehaviour, false, false, default, args);
        }

        public IUIEntity OpenUI(string UIName, Type xBehaviourType, params object[] args)
        {
            string ui_path = null;
            if (mUINameMode == UINameMode.UIGroup)
            {
                if (mCurUIGroup != null)
                {
                    if (!mCurUIGroup.TryGetPath(UIName, out ui_path))
                        throw new UIKitException("[TinaX.UIKit] Invalid UIName : " + UIName, UIKitErrorCode.InvalidUIName);
                }
            }
            else
                ui_path = (mUIRootDirLoadPath.IsNullOrEmpty()) ? UIName : mUIRootDirLoadPath_withSlash + UIName;

            if (ui_path.IsNullOrEmpty())
                throw new UIKitException("[TinaX.UIKit] " + (IsChinese ? $"未能获取到UI \"{UIName}\" 的加载路径，请检查设置或传入参数" : $"Cannot get UI Path by UI Name \"{UIName}\", Please check config or args."), UIKitErrorCode.ConnotGetUIPath);

            if(!xBehaviourType.IsAssignableFrom(typeof(XBehaviour)) && !xBehaviourType.IsSubclassOf(typeof(XBehaviour)))
                throw new UIKitException($"[TinaX.UIKit] Type {xBehaviourType.FullName} is invalid." , UIKitErrorCode.InvalidXBehaviourType);

            var xbehaviour = (XBehaviour)XCore.GetMainInstance().CreateInstance(xBehaviourType);
            return this.openUI(ui_path, UIName, null, xbehaviour, false, false, default, args);
        }

        public IUIEntity OpenUI(string UIName, XBehaviour behaviour, params object[] args)
        {
            string ui_path = null;
            if (mUINameMode == UINameMode.UIGroup)
            {
                if (mCurUIGroup != null)
                {
                    if (!mCurUIGroup.TryGetPath(UIName, out ui_path))
                        throw new UIKitException("[TinaX.UIKit] Invalid UIName : " + UIName, UIKitErrorCode.InvalidUIName);
                }
            }
            else
                ui_path = (mUIRootDirLoadPath.IsNullOrEmpty()) ? UIName : mUIRootDirLoadPath_withSlash + UIName;

            if (ui_path.IsNullOrEmpty())
                throw new UIKitException("[TinaX.UIKit] " + (IsChinese ? $"未能获取到UI \"{UIName}\" 的加载路径，请检查设置或传入参数" : $"Cannot get UI Path by UI Name \"{UIName}\", Please check config or args."), UIKitErrorCode.ConnotGetUIPath);

            return this.openUI(ui_path, UIName, null, behaviour, false, false, default, args);
        }

        public async Task<IUIEntity> OpenUIAsync<T>(string UIName, params object[] args) where T : XBehaviour
        {
            string ui_path = null;
            if (mUINameMode == UINameMode.UIGroup)
            {
                if (mCurUIGroup != null)
                {
                    if (!mCurUIGroup.TryGetPath(UIName, out ui_path))
                        throw new UIKitException("[TinaX.UIKit] Invalid UIName : " + UIName, UIKitErrorCode.InvalidUIName);
                }
            }
            else
                ui_path = (mUIRootDirLoadPath.IsNullOrEmpty()) ? UIName : mUIRootDirLoadPath_withSlash + UIName;

            if (ui_path.IsNullOrEmpty())
                throw new UIKitException("[TinaX.UIKit] " + (IsChinese ? $"未能获取到UI \"{UIName}\" 的加载路径，请检查设置或传入参数" : $"Cannot get UI Path by UI Name \"{UIName}\", Please check config or args."), UIKitErrorCode.ConnotGetUIPath);


            var xbehaviour = (XBehaviour)XCore.GetMainInstance().CreateInstance(typeof(T));
            var entity = await openUIAsync(ui_path, UIName, null, xbehaviour, false, false, default, args);
            return entity;
        }

        public async Task<IUIEntity> OpenUIAsync(string UIName, Type xBehaviourType, params object[] args)
        {
            string ui_path = null;
            if (mUINameMode == UINameMode.UIGroup)
            {
                if (mCurUIGroup != null)
                {
                    if (!mCurUIGroup.TryGetPath(UIName, out ui_path))
                        throw new UIKitException("[TinaX.UIKit] Invalid UIName : " + UIName, UIKitErrorCode.InvalidUIName);
                }
            }
            else
                ui_path = (mUIRootDirLoadPath.IsNullOrEmpty()) ? UIName : mUIRootDirLoadPath_withSlash + UIName;

            if (ui_path.IsNullOrEmpty())
                throw new UIKitException("[TinaX.UIKit] " + (IsChinese ? $"未能获取到UI \"{UIName}\" 的加载路径，请检查设置或传入参数" : $"Cannot get UI Path by UI Name \"{UIName}\", Please check config or args."), UIKitErrorCode.ConnotGetUIPath);
            
            if (!xBehaviourType.IsAssignableFrom(typeof(XBehaviour)) && !xBehaviourType.IsSubclassOf(typeof(XBehaviour)))
                throw new UIKitException($"[TinaX.UIKit] Type {xBehaviourType.FullName} is invalid.", UIKitErrorCode.InvalidXBehaviourType);

            var xbehaviour = (XBehaviour)XCore.GetMainInstance().CreateInstance(xBehaviourType);
            var entity = await openUIAsync(ui_path, UIName, null, xbehaviour, false, false, default, args);
            return entity;
        }

        public async Task<IUIEntity> OpenUIAsync(string UIName, XBehaviour behaviour, params object[] args)
        {
            string ui_path = null;
            if (mUINameMode == UINameMode.UIGroup)
            {
                if (mCurUIGroup != null)
                {
                    if (!mCurUIGroup.TryGetPath(UIName, out ui_path))
                        throw new UIKitException("[TinaX.UIKit] Invalid UIName : " + UIName, UIKitErrorCode.InvalidUIName);
                }
            }
            else
                ui_path = (mUIRootDirLoadPath.IsNullOrEmpty()) ? UIName : mUIRootDirLoadPath_withSlash + UIName;

            if (ui_path.IsNullOrEmpty())
                throw new UIKitException("[TinaX.UIKit] " + (IsChinese ? $"未能获取到UI \"{UIName}\" 的加载路径，请检查设置或传入参数" : $"Cannot get UI Path by UI Name \"{UIName}\", Please check config or args."), UIKitErrorCode.ConnotGetUIPath);

            
            var entity = await openUIAsync(ui_path, UIName, null, behaviour, false, false, default, args);
            return entity;
        }

        public void OpenUIAsync(string UIName, Type xBehaviourType, Action<IUIEntity,XException> callback,params object[] args)
        {
            this.OpenUIAsync(UIName, xBehaviourType, args)
                .ToObservable()
                .ObserveOnMainThread()
                .Subscribe(entity =>
                {
                    callback?.Invoke(entity, null);
                },
                e =>
                {
                    if (e is XException)
                        callback?.Invoke(null, e as XException);
                    else
                        Debug.LogException(e);
                });
        }

        public void OpenUIAsync(string UIName, XBehaviour behaviour, Action<IUIEntity, XException> callback, params object[] args)
        {
            this.OpenUIAsync(UIName, behaviour, args)
                .ToObservable()
                .ObserveOnMainThread()
                .Subscribe(entity =>
                {
                    callback?.Invoke(entity, null);
                },
                e =>
                {
                    if (e is XException)
                        callback?.Invoke(null, e as XException);
                    else
                        Debug.LogException(e);
                });
        }

        //------------UIName 和 

        #endregion

        public void CloseUI(UIEntity entity , params object[] args)
        {
            if (entity == null) return;
            this.closeUI(entity, args);
        }

        public void CloseUI(string UIName, params object[] args)
        {
            string ui_path = null;
            if (mUINameMode == UINameMode.UIGroup)
            {
                if (mCurUIGroup != null)
                {
                    if (!mCurUIGroup.TryGetPath(UIName, out ui_path))
                        throw new UIKitException("[TinaX.UIKit] Invalid UIName : " + UIName, UIKitErrorCode.InvalidUIName);
                }
            }
            else
            {
                ui_path = (mUIRootDirLoadPath.IsNullOrEmpty()) ? UIName : mUIRootDirLoadPath_withSlash + UIName;
            }

            if (ui_path.IsNullOrEmpty())
            {
                throw new UIKitException("[TinaX.UIKit] " + (IsChinese ? $"未能获取到UI \"{UIName}\" 的加载路径，请检查设置或传入参数" : $"Cannot get UI Path by UI Name \"{UIName}\", Please check config or args."), UIKitErrorCode.ConnotGetUIPath);
            }

            if(this.UIEntities.TryGetEntitys(ui_path, out var entities))
            {
                foreach (var item in entities)
                    this.CloseUI(item, args);
            }
        }


        /// <summary>
        /// 私有方法总入口
        /// </summary>
        /// <param name="UINameOrPath"></param>
        /// <param name="ui_root">只有在UI不是ScreenUI的情况下才需要传递这个值</param>
        private async Task<UIEntity> openUIAsync(string uiPath, string uiName, Transform ui_root, XComponent.XBehaviour xBehaviour, bool UseMask, bool CloseByMask, Color maskColor, params object[] args)
        {
            void setTop(UIEntity __entity)
            {
                //置顶
                if (mDict_UILayers.TryGetValue(__entity.SortingLayerID, out var layer))
                    layer.Top(__entity);
            }
            //加载检查
            if (this.UIEntities.TryGetEntitys(uiPath,out var entities))
            {
                if(entities.Length > 0)
                {
                    if(entities[0].UIStatue == UIStatus.Loaded && !entities[0].AllowMultiple)
                    {
                        setTop(entities[0]);
                        return entities[0];
                    }

                    if(entities[0].UIStatue == UIStatus.Loading)
                    {
                        await entities[0].OpenUITask;
                        if (!entities[0].AllowMultiple)
                        {
                            setTop(entities[0]);
                            return entities[0];
                        }
                    }
                }
            }

            //除了上面两种情况，其他都得重新加载
            UIEntity entity = new UIEntity(this, uiName, uiPath);
            entity.OpenUITask = doOpenUIAsync(entity, ui_root, xBehaviour, UseMask, CloseByMask, maskColor, args);
            this.UIEntities.Register(entity);

            await entity.OpenUITask;

            return entity;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="_ui_root">只有在UI不是ScreenUI的情况下才需要传递这个值</param>
        /// <returns></returns>
        private async Task doOpenUIAsync(UIEntity entity, Transform _ui_root, XComponent.XBehaviour xBehaviour,bool UseMask,bool CloseByMask, Color maskColor, params object[] args)
        {
            if (entity.UIStatue != UIStatus.Loaded && entity.UIStatue != UIStatus.Unloaded)
                entity.UIStatue = UIStatus.Loading;
            if (entity.UIGameObject == null)
            {
                var prefab = await Assets.LoadAsync<GameObject>(entity.UIPath);
                entity.UIPrefab = prefab;
                var uiPage = prefab.GetComponent<UIPage>();
                if (uiPage == null)
                {
                    string ui_name = entity.UIName;
                    Assets.Release(prefab);
                    throw new UIKitException("[TinaX.UIKit] " + (IsChinese ? $"无法打开UI \"{ui_name}\" , 这不是一个有效的UI页文件。" : $"Unable to open UI \"{ui_name}\", this is not a valid UI Page file."), UIKitErrorCode.InvalidUIPage);
                }
                if (uiPage.ScreenUI)
                {
                    //Screen UI， 使用UIKit Screen UIRoot
                    if (!SortingLayer.IsValid(uiPage.SortingLayerID))
                        uiPage.SortingLayerID = 0;
                    Transform trans_uiroot = getScreenUIRoot(uiPage.SortingLayerID);
                    entity.UIGameObject = UnityEngine.GameObject.Instantiate(prefab, trans_uiroot);
                    if (entity.UIGameObject.name.Length > 7)
                        entity.UIGameObject.Name(entity.UIGameObject.name.Substring(0, entity.UIGameObject.name.Length - 7));

                }
                else
                {
                    //非ScreenUI, 在指定的UIRoot下创建GameObject
                    entity.UIGameObject = UnityEngine.GameObject.Instantiate(prefab, _ui_root);
                    if (entity.UIGameObject.name.Length > 7)
                        entity.UIGameObject.Name(entity.UIGameObject.name.Substring(0, entity.UIGameObject.name.Length - 7));
                }
            }

            entity.UIPage = entity.UIGameObject.GetComponent<UIPage>();
            entity.UICanvas = entity.UIGameObject.GetComponentOrAdd<Canvas>();
            entity.UICanvas.sortingLayerID = entity.UIPage.SortingLayerID;
            if (entity.UIPage.ScreenUI)
            {
                entity.UICanvas.overrideSorting = true;
                //UI层级
                if (!mDict_UILayers.ContainsKey(entity.SortingLayerID))
                    mDict_UILayers.Add(entity.SortingLayerID, new UILayerManager());
                mDict_UILayers[entity.SortingLayerID].Register(entity);
            }

            //xbehaviour
            if (xBehaviour != null)
            {
                XCore.GetMainInstance().InjectObject(xBehaviour); //依赖注入，Services
                entity.UIPage.TrySetXBehavior(xBehaviour);
            }

            //mask
            if (UseMask)
                entity.ShowMask(CloseByMask, maskColor);

            //OpenUI事件
            if (args != null)
            {
                entity.UIPage.SendOpenUIMessage(args);
            }
            entity.UIStatue = UIStatus.Loaded;
            entity.OpenUITask = Task.CompletedTask;
        }

        private UIEntity openUI(string uiPath, string uiName, Transform ui_root, XComponent.XBehaviour xBehaviour, bool UseMask, bool CloseByMask, Color maskColor, params object[] args)
        {
            void setTop(UIEntity __entity)
            {
                //置顶
                if (mDict_UILayers.TryGetValue(__entity.SortingLayerID, out var layer))
                    layer.Top(__entity);
            }

            //加载检查
            if (this.UIEntities.TryGetEntitys(uiPath, out var entities))
            {
                if (entities.Length > 0)
                {
                    if (entities[0].UIStatue == UIStatus.Loaded && !entities[0].AllowMultiple)
                    {
                        setTop(entities[0]);
                        return entities[0];
                    }
                }
            }

            /*
             * 异步加载一个UI开始后，立即同步加载同一个UI,这种情况不管。约定不应该在开发的时候出现这种情况
             */

            UIEntity entity = new UIEntity(this, uiName, uiPath);

            if (entity.UIGameObject == null)
            {
                var prefab = Assets.Load<GameObject>(entity.UIPath);
                var uiPage = prefab.GetComponent<UIPage>();
                if (uiPage == null)
                {
                    string ui_name = entity.UIName;
                    Assets.Release(prefab);
                    throw new UIKitException("[TinaX.UIKit] " + (IsChinese ? $"无法打开UI \"{ui_name}\" , 这不是一个有效的UI页文件。" : $"Unable to open UI \"{ui_name}\", this is not a valid UI Page file."), UIKitErrorCode.InvalidUIPage);
                }
                if (uiPage.ScreenUI)
                {
                    //Screen UI， 使用UIKit Screen UIRoot
                    if (!SortingLayer.IsValid(uiPage.SortingLayerID))
                        uiPage.SortingLayerID = 0;
                    Transform trans_uiroot = getScreenUIRoot(uiPage.SortingLayerID);
                    entity.UIGameObject = UnityEngine.Object.Instantiate(prefab, trans_uiroot);
                    if (entity.UIGameObject.name.Length > 7)
                        entity.UIGameObject.Name(entity.UIGameObject.name.Substring(0, entity.UIGameObject.name.Length - 7));

                }
                else
                {
                    //非ScreenUI, 在指定的UIRoot下创建GameObject
                    entity.UIGameObject = UnityEngine.Object.Instantiate(prefab, ui_root);
                    if (entity.UIGameObject.name.Length > 7)
                        entity.UIGameObject.Name(entity.UIGameObject.name.Substring(0, entity.UIGameObject.name.Length - 7));
                }
                Assets.Release(prefab);
            }

            entity.UIPage = entity.UIGameObject.GetComponent<UIPage>();
            entity.UICanvas = entity.UIGameObject.GetComponentOrAdd<Canvas>();
            entity.UICanvas.sortingLayerID = entity.UIPage.SortingLayerID;
            if (entity.UIPage.ScreenUI)
            {
                entity.UICanvas.overrideSorting = true;
                //UI层级
                if (!mDict_UILayers.ContainsKey(entity.SortingLayerID))
                    mDict_UILayers.Add(entity.SortingLayerID, new UILayerManager());
                mDict_UILayers[entity.SortingLayerID].Register(entity);
            }

            //xbehaviour
            if (xBehaviour != null)
            {
                XCore.GetMainInstance().InjectObject(xBehaviour); //依赖注入，Services
                entity.UIPage.TrySetXBehavior(xBehaviour);
            }

            //mask
            if (UseMask)
                entity.ShowMask(CloseByMask, maskColor);

            //OpenUI事件
            if (args != null)
            {
                entity.UIPage.SendOpenUIMessage(args);
            }
            entity.UIStatue = UIStatus.Loaded;
            entity.OpenUITask = Task.CompletedTask;

            this.UIEntities.Register(entity);
            return entity;
        }

        /// <summary>
        /// 关闭UI  私有方法总入口
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="args"></param>
        private void closeUI(UIEntity entity, params object[] args)
        {
            //退还layer
            if(mDict_UILayers.TryGetValue(entity.SortingLayerID,out var layer))
            {
                layer.Remove(entity);
            }
            //移除登记
            UIEntities.Remove(entity);
            //传递参数
            if (args != null)
                entity.UIPage.SendCloseUIMessage(args);
            //
            entity.Dispose();
            entity = null;
        }

        private Transform getScreenUIRoot(int sortingLayerId)
        {
            if (mDict_UIRoot_SortingLayer_Go.TryGetValue(sortingLayerId, out var go))
                return go.transform;
            else
            {
                refreshUIRoot(sortingLayerId);
                return mDict_UIRoot_SortingLayer_Go[sortingLayerId].transform;
            }
        }

        private void refreshUIRoot(int sortingLayerId)
        {
            if (SortingLayer.IsValid(sortingLayerId))
            {
                GameObject ui_root_go;
                if (!mDict_UIRoot_SortingLayer_Go.TryGetValue(sortingLayerId, out ui_root_go))
                {
                    ui_root_go = mUIKit_Root_Go.FindOrCreateGameObject("UIRoot_" + sortingLayerId,
                        typeof(Canvas),
                        typeof(CanvasScaler),
                        typeof(GraphicRaycaster))
                        .SetLocalPosition(Vector3.zero);
                    mDict_UIRoot_SortingLayer_Go.Add(sortingLayerId, ui_root_go);
                }

                var canvas = ui_root_go.GetComponentOrAdd<Canvas>();
                if (!mDict_UIRootCanvas_SortingLayer_Canvas.ContainsKey(sortingLayerId))
                    mDict_UIRootCanvas_SortingLayer_Canvas.Add(sortingLayerId, canvas);

                canvas.sortingLayerID = sortingLayerId;
                if(mScreenUICamera == null)
                {
                    canvas.worldCamera = null;
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                else
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = mScreenUICamera;
                }
            }
        }




        private static bool? _ischinese;
        private static bool IsChinese
        {
            get
            {
                if (_ischinese == null)
                {
                    _ischinese = (Application.systemLanguage == SystemLanguage.Chinese || Application.systemLanguage == SystemLanguage.ChineseSimplified);
                }
                return _ischinese.Value;
            }
        }
    }
}

