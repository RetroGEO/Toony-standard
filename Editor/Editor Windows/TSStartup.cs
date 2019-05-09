using UnityEngine;
using UnityEditor;
using System.IO;

namespace Cibbi.ToonyStandard
{
    /// <summary>
    /// Class used to automatically check an update every time the unity editor loads
    /// </summary>
    [InitializeOnLoad]
    public class TSStartup
    {
        private static TSUpdater updater;
        private static TSAutoUpdatePopup window;
        /// <summary>
        /// Static contructor that runs on editor load and starts the update check
        /// </summary>
        static TSStartup ()
        {
            bool update;

            string path="";
            string[] logo = AssetDatabase.FindAssets("ToonyStandardLogo t:Texture2D", null);
            if (logo.Length > 0)
            {
                string[] pieces = AssetDatabase.GUIDToAssetPath(logo[0]).Split('/');
                ArrayUtility.RemoveAt(ref pieces, pieces.Length - 1);
                ArrayUtility.RemoveAt(ref pieces, pieces.Length - 1);
                ArrayUtility.RemoveAt(ref pieces, pieces.Length - 1);
                path = string.Join("/", pieces);
            }

            if(!path.Equals(TSConstants.LocalShaderAssetsFolder))
            {
                AssetDatabase.MoveAsset(path,TSConstants.LocalShaderAssetsFolder);
            }


            //checks if there's old configuration settings that needs to be reimported 
            if(File.Exists(TSConstants.oldSettingsJSONPath))
			{
				TSSettings settings=JsonUtility.FromJson<TSSettings>(File.ReadAllText(TSConstants.oldSettingsJSONPath));
				File.WriteAllText(TSConstants.settingsJSONPath,JsonUtility.ToJson(settings));
                File.Delete(TSConstants.oldSettingsJSONPath);
                AssetDatabase.Refresh();
			}

            if(File.Exists(TSConstants.settingsJSONPath))
			{
				TSSettings settings=JsonUtility.FromJson<TSSettings>(File.ReadAllText(TSConstants.settingsJSONPath));
				update=!settings.disableUpdates;
			}
			else
			{
				TSSettings settings=new TSSettings();
				settings.sectionStyle=(int)SectionStyle.Bubbles;
				settings.sectionColor=new Color(1,1,1,1);
				settings.disableUpdates=false;
				File.WriteAllText(TSConstants.settingsJSONPath,JsonUtility.ToJson(settings));
                update=true;
			}

            if(!EditorPrefs.HasKey(TSConstants.TSEPNotFirstTime))
            {
                int windowWidth=500;
                int windowHeight=500; 
                TSFirstTimeWindow ftWindow = EditorWindow.CreateInstance<TSFirstTimeWindow>();
                ftWindow.minSize=new Vector2(windowWidth, windowHeight);
			    ftWindow.maxSize=new Vector2(windowWidth, windowHeight);
                ftWindow.titleContent=new GUIContent("Welcome to Toony Standard!");
                ftWindow.ShowUtility(); 
            }

            if(update)
            {
                updater=new TSUpdater();
                updater.StartCoroutine(updater.CheckForUpdate()); 
                // This will make the update function continously running each update
                EditorApplication.update += Update;
            }
        }
        /// <summary>
        /// Static update function that is passed to the editor on load and is responsible for handling the updater states
        /// </summary>
        static void Update ()
        {
            updater.Update();

            switch(updater.GetState())
            {
                case UpdaterState.Fetching:
                    break;
                // If the updater found an update a popup window is created to inform the user that an update is available
                case UpdaterState.Ready:
                    if(window==null)
                    {
                        Debug.Log("Toony standard: new update found");
                        int windowWidth=500;
                        int windowHeight=320; 
                        window = EditorWindow.CreateInstance<TSAutoUpdatePopup>();
                        window.updater=updater;
                        window.update=Update;
                        window.minSize=new Vector2(windowWidth, windowHeight);
			            window.maxSize=new Vector2(windowWidth, windowHeight);
                        window.titleContent=new GUIContent("Toony Standard Updater");
                        window.ShowUtility(); 
                    } 

                    break;

                case UpdaterState.UpToDate:
                    Debug.Log("Toony standard: shader is up to date");
                    EditorApplication.update -= Update;
                    break;
                case UpdaterState.Error:
                    Debug.Log("Toony standard: error on getting update information");
                    EditorApplication.update -= Update;
                    break;

                case UpdaterState.Downloaded:
                    window.Close();
                    EditorApplication.update -= Update;
                    break; 

            }
        }
    }

    /// <summary>
    /// Popup window used to inform the user that an update is available
    /// </summary>
    public class TSAutoUpdatePopup : EditorWindow
    {
        public TSUpdater updater;
        public EditorApplication.CallbackFunction update;


        void OnGUI() 
        {
            TSFunctions.DrawHeader(20); 
            updater.DrawGUI();
        }

        public void OnInspectorUpdate()
		{
			if(updater != null)
			{	
				Repaint();			
			}
		}
        void OnDestroy()
        {
            EditorApplication.update-=update; 
        }

    }

    /// <summary>
    /// Popup window used to make people set
    /// </summary>
    public class TSFirstTimeWindow : EditorWindow
    {
        InspectorLevel inspectorLevel=InspectorLevel.Normal;

        string boxMessage="Normal level will give the vast majority of the features this shader has to offer, this is the default setting and probably will be the most used one."; 

        void OnGUI() 
        {
            TSFunctions.DrawHeader(20); 
            EditorGUILayout.LabelField("Seems like this is your first time installing Toony Standard, first of all, thanks for using it, it makes me happy.",TSConstants.Styles.multilineLabel);
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Now, the only thing that you need to set immediately is the inspector level, this will tell the shader what features to expose based on your experience with making stuff in unity.",TSConstants.Styles.multilineLabel);
            GUILayout.Space(10);
            EditorGUILayout.LabelField("You can edit this choise later and modify other options by going on Window/Toony Standard/Settings ",TSConstants.Styles.multilineLabel);
            GUILayout.Space(10);
            EditorGUI.BeginChangeCheck();
			inspectorLevel = (InspectorLevel)EditorGUILayout.EnumPopup(TSConstants.TSWindowLabels.InspectorLevel,inspectorLevel);
			if(EditorGUI.EndChangeCheck())
			{
				EditorPrefs.SetInt(TSConstants.TSEPInspectorLevel,(int)inspectorLevel);

                switch (inspectorLevel)
                {
                    case InspectorLevel.Basic:
                        boxMessage="The basic level is suited for people who are relatively new to avatar creation giving them just the basic stuff they need to get started.";
                        break;
                    case InspectorLevel.Normal:
                        boxMessage="Normal level will give the vast majority of the features this shader has to offer, this is the default setting and probably will be the most used one.";
                        break;
                    case InspectorLevel.Expert:
                         boxMessage="Warning, i expect you to be experienced in what you're doing in order to select this.\nThis level grants access to all the features and little adjustments that you can do with this shader, even if you don't know what the hell they do.";
                        break;   
                }
			}

            EditorGUILayout.HelpBox(boxMessage,MessageType.Info);

            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("Done!",GUILayout.MinWidth(100)))
            {
                this.Close();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);


        }

        void OnDestroy()
        {
            EditorPrefs.SetBool(TSConstants.TSEPNotFirstTime,true); 
        }


    }
}