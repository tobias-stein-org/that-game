using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace tg.ui.view
{
    using tg.events;

    public interface IViewController
    {
        void activated(VisualElement view);
        void deactivated();
    }

    public class Controller : ScriptableObject
    {
        public string assemblyReference;

#if UNITY_EDITOR

		/// <summary>
        /// Some editor magic, to automatically generate scriptable object assets from any defined 'UIController' class.
        /// </summary>
		//[UnityEditor.InitializeOnLoadMethod]
        [MenuItem("tg/UI/Refresh Controller")]
		static void OnProjectLoadedInEditor()
		{
            //AssetDatabase.Refresh();
            AssetDatabase.DisallowAutoRefresh();

			// Determine all defined event types
			List<Type> UIControllerClasses = new List<Type>();
            {
                Type TIViewController = typeof(IViewController);

                // Get event types from all currently referenced assemblies.
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // get all defined event types in this assembly
                    List<Type> UIControllers = asm
                        // get all types from this assembly
                        .GetTypes()
                        .Where(T => T.IsClass && T.GetInterfaces().Contains(TIViewController))
                        .ToList();

                    // append found event types in the current assembly
                    UIControllerClasses.AddRange(UIControllers);
                }
            }

			// make sure 'Assets/UI/Controller' folder exists
            // note all asset path are separated by '/'
			string[] assetPaths = AssetDatabase.GetAllAssetPaths();


            if (!assetPaths.Contains("Assets/UI")) { AssetDatabase.CreateFolder("Assets", "UI"); }
            if (!assetPaths.Contains("Assets/UI/Controllers")) { AssetDatabase.CreateFolder("Assets/UI", "Controllers"); }


            List<string> existingUIControllers = AssetDatabase.FindAssets("", new string[] { "Assets/UI/Controllers/" }).Select(asset => AssetDatabase.GUIDToAssetPath(asset)).ToList();

            foreach (Type UIControllerClass in UIControllerClasses)
            {
                string uiControllerAssetpath = $"Assets/UI/Controllers/{UIControllerClass.Name}.asset";

                Controller controllerAsset = null;

                // check if controller already exists
                if(existingUIControllers.Contains(uiControllerAssetpath))
                {
                    existingUIControllers.Remove(uiControllerAssetpath);
                    controllerAsset = (Controller)AssetDatabase.LoadMainAssetAtPath(uiControllerAssetpath);
                }
                else
                {
                    // else we will create it
                    controllerAsset = ScriptableObject.CreateInstance<Controller>();
                    AssetDatabase.CreateAsset(controllerAsset, uiControllerAssetpath);
                }

                controllerAsset.assemblyReference  = UIControllerClass.AssemblyQualifiedName;
            }

            // remove any assets for old ui controllers, which are not present anymore
            AssetDatabase.DeleteAssets(existingUIControllers.ToArray(), new List<string>());
            AssetDatabase.SaveAssets();

            AssetDatabase.AllowAutoRefresh();
            AssetDatabase.Refresh();
        }

#endif // UNITY_EDITOR
    }
}
