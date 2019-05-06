using UnityEngine;
using UnityEditor;
using System.Collections;
using UnityEngine.Networking;
using System;
using System.Collections.Generic;
using Yallie.Unzip;
using System.IO;
using Cibbi.ToonyStandard.GithubAPI;

namespace Cibbi.ToonyStandard
{
    /// <summary>
    /// update streams the updater has to look at
    /// </summary>
    public enum UpdateStream
    {
        Release,
        Beta		
    }

    /// <summary>
    /// possible updater object states
    /// </summary>
    public enum UpdaterState
    {
        Waiting,
        Fetching,
        Ready,
        UpToDate,
        Downloading,
        Downloaded,
        Error
    }

    /// <summary>
    /// Window independent updater class
    /// </summary>
    public class TSUpdater{

        public UpdateStream updateStream;
        private UpdaterState state;

        string errorMessage="Couldn't retrieve update information, please retry later";

        UnityWebRequest request;
        DownloadHandlerBuffer response;
        DownloadHandlerFile file;

        GithubReleaseJSON githubReleaseJSON;
        GithubCommitJSON githubBetaJSON;

        List<TSTimedCoroutine> timedCoroutines;
        DateTime previousTimeSinceStartup;

        Texture2D icon;

        Vector2 MainAreaScrollPos;
        /// <summary>
        /// Constructor for the TSUpdater class
        /// </summary>
        public TSUpdater()
        {
            state=UpdaterState.Waiting;
            timedCoroutines=new List<TSTimedCoroutine>();
            previousTimeSinceStartup = DateTime.Now;
            
            LocalVersionJSON local;
            if(File.Exists(TSConstants.localJSONPath))
            {
                local=JsonUtility.FromJson<LocalVersionJSON>(File.ReadAllText(TSConstants.localJSONPath));
            }
            else
            {
                local = new LocalVersionJSON();
                local.beta=true;
                local.betaSha="";
                local.version="beta";
                File.WriteAllText(TSConstants.localJSONPath,JsonUtility.ToJson(local));
            }

            if(local.beta)
            {
                updateStream=UpdateStream.Beta;
            }
            else
            {
                updateStream=UpdateStream.Release;
            }
        }

        /// <summary>
        /// Update function for the TSUpdater class
        /// </summary>
        public void Update()
        {
            float deltaTime = (float) (DateTime.Now.Subtract(previousTimeSinceStartup).TotalMilliseconds / 1000.0f);
            previousTimeSinceStartup = DateTime.Now;

            if(timedCoroutines.Count>0)
            {
                for (int i = 0; i < timedCoroutines.Count; i++)
                {
                    
                    TSTimedCoroutine coroutine=timedCoroutines[i];
                    if(coroutine.isDone(deltaTime))
                    {
                        if(coroutine.MoveNext())
                        {
                            coroutine.resetTimer();
                        }
                        else
                        {
                            timedCoroutines.Remove(coroutine);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Shows optional GUI elements that are dependent on the updater state
        /// </summary>
        public void DrawGUI()
        {
            switch (state)
            {
                case UpdaterState.Waiting:
                    if(GUILayout.Button("Check for Update"))
                    {     
                        StartCoroutine(CheckForUpdate());         
                    }
                    break;

                case UpdaterState.Fetching:
                    EditorGUILayout.LabelField("Checking for update");
                    break;

                case UpdaterState.Ready:
                    if(updateStream==UpdateStream.Beta)
                    {
                        EditorGUILayout.LabelField("New beta update found");
                        EditorGUILayout.BeginVertical("box");
                        MainAreaScrollPos=EditorGUILayout.BeginScrollView(MainAreaScrollPos, GUILayout.MinHeight(100),GUILayout.MaxHeight(400)); 
                        EditorGUILayout.LabelField(githubBetaJSON.commit.message);               
                        EditorGUILayout.EndScrollView();
                        EditorGUILayout.EndVertical();
                    }
                    else
                    {
                        EditorGUILayout.LabelField("New version found: "+githubReleaseJSON.tag_name);
                        EditorGUILayout.BeginVertical("box");
                        MainAreaScrollPos=EditorGUILayout.BeginScrollView(MainAreaScrollPos, GUILayout.MinHeight(100),GUILayout.MaxHeight(400)); 
                        EditorGUILayout.LabelField(githubReleaseJSON.body);               
                        EditorGUILayout.EndScrollView();
                        EditorGUILayout.EndVertical();
                    }
                    if(GUILayout.Button("Update"))
                    {     
                        StartCoroutine(DownloadUpdate());         
                    }
                    break;
                
                case UpdaterState.UpToDate:
                    EditorGUILayout.LabelField("Your Toony Standard installation is up to date!");
                    break;

                case UpdaterState.Downloading:
                    if(updateStream==UpdateStream.Beta)
                    {   
                        EditorGUILayout.LabelField("New beta update found");
                        EditorGUILayout.BeginVertical("box");
                        MainAreaScrollPos=EditorGUILayout.BeginScrollView(MainAreaScrollPos, GUILayout.MinHeight(100),GUILayout.MaxHeight(100)); 
                        EditorGUILayout.LabelField(githubBetaJSON.commit.message);               
                        EditorGUILayout.EndScrollView();
                        EditorGUILayout.EndVertical();
                        EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false),request.downloadProgress,(request.downloadProgress*100)+"%");
                    }
                    else
                    {
                        EditorGUILayout.LabelField("New version found: "+githubReleaseJSON.tag_name);
                        EditorGUILayout.BeginVertical("box");
                        MainAreaScrollPos=EditorGUILayout.BeginScrollView(MainAreaScrollPos, GUILayout.MinHeight(100),GUILayout.MaxHeight(100)); 
                        EditorGUILayout.LabelField(githubReleaseJSON.body);               
                        EditorGUILayout.EndScrollView();
                        EditorGUILayout.EndVertical();
                        EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false),request.downloadProgress,(request.downloadProgress*100)+"%");
                    }
                    
                    break;

                case UpdaterState.Error:
                    EditorGUILayout.LabelField(errorMessage);
                    break;

            }
        }

        /// <summary>
        /// Coroutine that checks if there is a new update available
        /// </summary>
        /// <returns></returns>
        public IEnumerator<float> CheckForUpdate()
        {
            LocalVersionJSON local;
            // Checks if there's a dev file in the project to skip the update check
            if(File.Exists(Application.dataPath+"/DevCheck.json"))
            {
                local=JsonUtility.FromJson<LocalVersionJSON>(File.ReadAllText(Application.dataPath+"/DevCheck.json"));
                if(local.version.Equals("dev"))
                {
                    state=UpdaterState.UpToDate;
                    yield break;
                }
                local=null;
            }
            // Checks if the version json is present and creates a new one that will trigger an update if not present
            if(File.Exists(TSConstants.localJSONPath))
            { 
                local=JsonUtility.FromJson<LocalVersionJSON>(File.ReadAllText(TSConstants.localJSONPath));
            }
            else
            {
                local = new LocalVersionJSON();
                local.beta=true;
                local.betaSha="";
                local.version="release";
                File.WriteAllText(TSConstants.localJSONPath,JsonUtility.ToJson(local));
            }
            
            // Creates a web request to the github api dependent to the update stream currently selected
            if(updateStream==UpdateStream.Beta)
            {
                request= new UnityWebRequest("https://api.github.com/repos/Cibbi/Toony-standard/commits/master");
            }
            else
            {
                request= new UnityWebRequest("https://api.github.com/repos/Cibbi/Toony-standard/releases/latest");
            }
            request.method=UnityWebRequest.kHttpVerbGET;
            response = new DownloadHandlerBuffer();
            request.downloadHandler = response;
            request.SendWebRequest();
            state = UpdaterState.Fetching;
            // Main check cycle that waits for a response from the api, execution of this part is paused every cycle for 0.5 seconds in order to avoid
            // to block the normal window execution
            while(state == UpdaterState.Fetching)
            {
                yield return 0.5f;
                // Executed if the request got an error response
                if(request.isHttpError||request.isNetworkError)
                {
                    state = UpdaterState.Error;
                    Debug.Log(request.error);
                }
                // Executed if the request is done
                if(request.isDone)
                {   
                    if(updateStream==UpdateStream.Beta)
                    {
                        githubBetaJSON=JsonUtility.FromJson<GithubCommitJSON>(response.text);
                        if(local.beta&&local.betaSha==githubBetaJSON.sha)
                        {
                            state=UpdaterState.UpToDate;
                        }
                        else if(local.betaSha.Equals("nosha"))
                        {
                            local.betaSha=githubBetaJSON.sha;
                            File.WriteAllText(TSConstants.localJSONPath,JsonUtility.ToJson(local));
                            state=UpdaterState.UpToDate;
                        }
                        else
                        {
                            state=UpdaterState.Ready;
                        }
                    }
                    else
                    {
                        githubReleaseJSON=JsonUtility.FromJson<GithubReleaseJSON>(response.text);
                        if(!local.beta&&local.version.Equals(githubReleaseJSON.tag_name))
                        {
                            state=UpdaterState.UpToDate;
                        }
                        else
                        {
                            state=UpdaterState.Ready;
                        }
                    }
                    
                }
            }
        }

        /// <summary>
        /// Coroutine that downloads the update file and installs it
        /// </summary>
        /// <returns></returns>
        public IEnumerator<float> DownloadUpdate()
        {
            if(state==UpdaterState.Ready)
            {
                // Creates a web request to the github repository based on the selected update stream
                if(updateStream==UpdateStream.Beta)
                {
                    request = new UnityWebRequest("https://github.com/Cibbi/Toony-standard/archive/"+githubBetaJSON.sha+".zip");        
                    file= new DownloadHandlerFile(Application.dataPath+"/toonyStandard.zip");
                }
                else
                {
                    request = new UnityWebRequest(githubReleaseJSON.assets[0].browser_download_url);        
                    file= new DownloadHandlerFile(Application.dataPath+"/toonyStandard.unitypackage");
                }
                
                request.method=UnityWebRequest.kHttpVerbGET;
                file.removeFileOnAbort=true;
                request.downloadHandler=file;
                request.SendWebRequest();
                state=UpdaterState.Downloading;
                // Main check cycle that waits for the downloaded file, like the update check execution is paused every cycle to not block
                // normal window execution
                while(state == UpdaterState.Downloading)
                {
                    yield return 0.5f;
                    // Executed if the request is done
                    if(request.isDone)
                    {
                        state=UpdaterState.Downloaded;

                        TSSettings settings=JsonUtility.FromJson<TSSettings>(File.ReadAllText(TSConstants.settingsJSONPath));

                        // If the update stream is the beta one the downloaded file is a zip file, meaning that we have to extract it manually, fortunately a guy called Yallie made a simple
                        // extraction class that handles the basic stuff needed here, check him over https://github.com/yallie/unzip
                        if(updateStream==UpdateStream.Beta)
                        {   
                            // Deleting the old Toony standard version
                            if(Directory.Exists(Application.dataPath+"/Cibbi's shaders/Toony standard"))
                            {
                                Directory.Delete(Application.dataPath+"/Cibbi's shaders/Toony standard",true);
                            }
                            Unzip zip= new Unzip(Application.dataPath+"/toonyStandard.zip");
                            // For each file in the zip we change the github repository path with the more user friendly one used on the releases, and then extract that file in that path
                            foreach (string fileName in zip.FileNames)
                            {
                                string newDir = Application.dataPath + fileName.Replace("Toony-standard-"+githubBetaJSON.sha,"/Cibbi's shaders/Toony standard");
                                if(!Directory.Exists(Path.GetDirectoryName(newDir))) 
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(newDir));
                                }
                                zip.Extract(fileName,newDir);
                            }
                            // Disposing of the zip, this is important cause without doing it the zip file cannot be deleted afterwards
                            zip.Dispose();
                            // Creation of the updated version.json file for this beta version, cause the one that comes in the zip does not contain the sha of the commit used when checking updates
                            // Since it's impossible to know a commit sha before doing such commit.
                            LocalVersionJSON local = new LocalVersionJSON();
                            local.beta=true;
                            local.betaSha=githubBetaJSON.sha;
                            local.version="beta";
                            File.WriteAllText(TSConstants.localJSONPath,JsonUtility.ToJson(local));
                            // The asset database is refreshed to be sure that the zip file is actually detected from the asset database for its deletion
                            AssetDatabase.Refresh(ImportAssetOptions.ImportRecursive);
                            AssetDatabase.DeleteAsset("Assets/toonyStandard.zip");
                            
                        }
                        // If the update stream is the release one the downloaded file is the latest unitypackage that can be found here https://github.com/Cibbi/Toony-standard/releases
                        // Since it's a unitypackage its installation is relatively easy, but we still delete the old version first for safety
                        else
                        {
                            if(Directory.Exists(Application.dataPath+"/Cibbi's shaders/Toony standard"))
                            {
                                Directory.Delete(Application.dataPath+"/Cibbi's shaders/Toony standard",true);
                            }
                            AssetDatabase.ImportPackage(Application.dataPath+"/toonyStandard.unitypackage",false);
                            AssetDatabase.Refresh();
                            AssetDatabase.DeleteAsset("Assets/toonyStandard.unitypackage");
                        }

                        File.WriteAllText(TSConstants.oldSettingsJSONPath,JsonUtility.ToJson(settings));
                        
                    }
                    // Executed if the request got an error response
                    if(request.isNetworkError||request.isHttpError)
                    {
                        Debug.Log("Toony Standard: network error during downlaod, please retry later");
                    }
                }
            }
        }
    
        /// <summary>
        /// Used to add a coroutine to the list of running coroutines
        /// </summary>
        /// <param name="coroutine"></param>
        public void StartCoroutine(IEnumerator<float> coroutine)
        {
            timedCoroutines.Add(new TSTimedCoroutine(coroutine));
        }

        /// <summary>
        /// Returns the state of the updater
        /// </summary>
        /// <returns>UpdaterState enum containing the current updater state</returns>
        public UpdaterState GetState()
        {
            return state;
        }

        /// <summary>
        /// Resets the updater, so it can run an update check again
        /// </summary>
        public void Reset()
        {
            state=UpdaterState.Waiting;
        }

        /// <summary>
        /// Tells if the ui should be redrawn periodically, not too useful honestly
        /// </summary>
        /// <returns>A bool that says if the gui should be redrawn periodically</returns>
        public bool ShouldGUIBeRedrawn()
        {
            switch (state)
            {
                case UpdaterState.Waiting:
                case UpdaterState.Ready:
                case UpdaterState.UpToDate:
                case UpdaterState.Downloaded:
                case UpdaterState.Error:
                    return false;

                case UpdaterState.Fetching:
                case UpdaterState.Downloading:
                    return true;

                default:
                    return false;
            }


        }
        
    }

    /// <summary>
    /// Class used for getting the local version.json file info using JsonUtility
    /// </summary>
    [Serializable]
    public class LocalVersionJSON
    {
        public string version;
        public bool beta;
        public string betaSha;
    }

    /// <summary>
    /// Class designed for bringing some simple Coroutine functionalities outside of monobehavious
    /// </summary>
    public class TSTimedCoroutine
    {
        private IEnumerator<float> coroutine;
        private float timeLeft;

        public TSTimedCoroutine(IEnumerator<float> coroutine)
        {
            this.coroutine=coroutine;
            timeLeft=this.coroutine.Current;
        }

        public bool MoveNext()
        {
            return coroutine.MoveNext();
        }

        public void resetTimer()
        {
            timeLeft=coroutine.Current;
        }

        public bool isDone(float deltaTime)
        {
            timeLeft -= deltaTime;
            return timeLeft < 0;
        }

    }

    /// <summary>
    /// Class used to automatically check an update every time the unity editor loads
    /// </summary>
    [InitializeOnLoad]
    public class TSAutoUpdater
    {
        private static  TSUpdater updater;
        private static TSAutoUpdatePopup window;
        /// <summary>
        /// Static contructor that runs on editor load and starts the update check
        /// </summary>
        static TSAutoUpdater ()
        {
            bool update;

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
        private Texture2D icon;
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
}
