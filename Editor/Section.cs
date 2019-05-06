using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using System.IO;

namespace Cibbi.ToonyStandard
{
    public delegate void SectionContent(MaterialEditor materialEditor);
    public delegate void ChangesCheck(bool isOpen, bool isEnabled);

    public class Section
    {
        private bool isOpen;
        private bool isEnabled;
        private GUIContent sectionTitle;
        private SectionContent content;
        private ChangesCheck changesCheck;
        private Color sectionBgColor;
        private SectionStyle sectionStyle;

        public Section(GUIContent sectionTitle, bool open, SectionContent content, ChangesCheck changesCheck)
        {
            this.sectionTitle = sectionTitle;
            this.isOpen = open;
            this.content = content;
            this.changesCheck = changesCheck;

            TSSettings settings=JsonUtility.FromJson<TSSettings>(File.ReadAllText(TSConstants.settingsJSONPath));
			sectionStyle=(SectionStyle)settings.sectionStyle;
			this.sectionBgColor=settings.sectionColor;
        }

        public void DrawSection(MaterialEditor materialEditor)
        {
            isEnabled=true;
            EditorGUI.BeginChangeCheck();
            Color bCol = GUI.backgroundColor;
            GUI.backgroundColor = sectionBgColor;
            switch(sectionStyle)
            {
                case SectionStyle.Bubbles:
                    drawBubblesSection(bCol);
                    break;
                case SectionStyle.Foldout:
                    drawFoldoutSection(bCol);
                    break;
                case SectionStyle.Box:
                    drawBoxSection(bCol);
                    break;
            }
            if (isOpen)
            {
                content(materialEditor);
            }
            if(sectionStyle==SectionStyle.Bubbles)
            {
                EditorGUILayout.EndVertical();
            }
            //
            //EditorGUILayout.Space();

            if (EditorGUI.EndChangeCheck())
            {
                changesCheck(isOpen, isEnabled);
            }
        }

        private void drawBubblesSection(Color bCol)
        {
            EditorGUILayout.BeginVertical("Button");
            GUI.backgroundColor = bCol;
            Rect r = EditorGUILayout.BeginHorizontal();
            isOpen=EditorGUILayout.Toggle(isOpen, EditorStyles.foldout, GUILayout.MaxWidth(15.0f));
            EditorGUILayout.LabelField(sectionTitle, TSConstants.Styles.sectionTitleCenter);      
            isEnabled=EditorGUILayout.Toggle(isEnabled, TSConstants.Styles.deleteStyle, GUILayout.MaxWidth(15.0f));
            isOpen = GUI.Toggle(r, isOpen, GUIContent.none, new GUIStyle());  
            EditorGUILayout.EndHorizontal();
        }

        private void drawFoldoutSection(Color bCol)
        {
            TSFunctions.DrawLine(new Color(0.35f,0.35f,0.35f,1),1,0);
            GUI.backgroundColor = bCol;
            
            Rect r = EditorGUILayout.BeginHorizontal();
            isOpen=EditorGUILayout.Toggle(isOpen, EditorStyles.foldout, GUILayout.MaxWidth(15.0f));
            EditorGUILayout.LabelField(sectionTitle, TSConstants.Styles.sectionTitle);      
            isEnabled=EditorGUILayout.Toggle(isEnabled, TSConstants.Styles.deleteStyle, GUILayout.MaxWidth(15.0f));
            isOpen = GUI.Toggle(r, isOpen, GUIContent.none, new GUIStyle());  
            EditorGUILayout.EndHorizontal();
        }

        private void drawBoxSection(Color bCol)
        {
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = bCol;
            
            Rect r = EditorGUILayout.BeginHorizontal();
            isOpen=EditorGUILayout.Toggle(isOpen, EditorStyles.foldout, GUILayout.MaxWidth(15.0f));
            EditorGUILayout.LabelField(sectionTitle, TSConstants.Styles.sectionTitle);      
            isEnabled=EditorGUILayout.Toggle(isEnabled, TSConstants.Styles.deleteStyle, GUILayout.MaxWidth(15.0f));
            isOpen = GUI.Toggle(r, isOpen, GUIContent.none, new GUIStyle());  
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        public GUIContent getSectionTitle()
        {
            return sectionTitle;
        }

    }
}