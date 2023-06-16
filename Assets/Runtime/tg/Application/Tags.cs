using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEditor;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;

namespace tg.application
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack=1)]
    public struct TagHash
    {
        private int value;

        public static implicit operator TagHash(int value) { return new TagHash { value = value }; }
        public static implicit operator TagHash(string value) { return value.GetHashCode(); }
        public static implicit operator int(TagHash value) { return value.value; }

    }

    /// <summary>
    /// Simple Tag
    /// </summary>
    public struct Tag
    {
        public readonly string  name;
        public readonly TagHash hash;

        public Tag(string name)
        {
            this.name   = name;
            this.hash   = name.GetHashCode();
        }

        public override int GetHashCode() { return hash; }
        public override bool Equals(object obj) { return obj is Tag tag && hash == tag.hash; }

        public static bool operator ==(in Tag tag, TagHash hash) { return tag.hash  == hash; }
        public static bool operator !=(in Tag tag, TagHash hash) { return tag.hash  != hash; }
        public static bool operator ==(in Tag tag0, in Tag tag1) { return tag0.hash == tag1.hash; }
        public static bool operator !=(in Tag tag0, in Tag tag1) { return tag0.hash != tag1.hash; }
    }

    /// <summary>
    /// Contains all tg.application wide used tags.
    /// </summary>
    public static class tags
    {
        public  static readonly Tag Camera       = new Tag("Camera");
        public  static readonly Tag Player       = new Tag("Player");
        public  static readonly Tag Enemy        = new Tag("Enemy");
        public  static readonly Tag Level        = new Tag("Level");
        // add more tags here ...

        private static readonly Dictionary<TagHash, string> tagLookup;

        public  static string hash2Name(int hash)
        {
            if(tags.tagLookup.TryGetValue(hash, out string name))
            {
                return name;
            }

            return "INVALID_TAG";
        }
        static tags()
        {
            var TTag        = typeof(Tag);
            tags.tagLookup  = typeof(tags).GetFields(BindingFlags.Public | BindingFlags.Static).Where(info => info.FieldType == TTag).Select(info => (Tag)info.GetValue(null)).ToDictionary(tag => tag.hash, tag => tag.name);
        }

#if UNITY_EDITOR

        [InitializeOnLoadMethod]
        private static void syncTags()
        {
            var TTag = typeof(Tag);

            SerializedObject    tagManager  = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty  tags        = tagManager.FindProperty("tags");

            foreach(var tag in typeof(tags).GetFields(BindingFlags.Public | BindingFlags.Static).Where(info => info.FieldType == TTag).Select(info => (Tag)info.GetValue(null)))
            {
                // if not found, add it
                if (!propertyExists(tags, tag.name))
                {
                    int index               = tags.arraySize;
                    tags.InsertArrayElementAtIndex(index);
                    SerializedProperty sp   = tags.GetArrayElementAtIndex(index);
                    sp.stringValue          = tag.name;

                    Debug.Log($"Tag: '{tag.name}' has been added");
                }
            }

            tagManager.ApplyModifiedProperties();
        }

        private static bool propertyExists(SerializedProperty property,string value)
        {
            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).stringValue.Equals(value))
                {
                    return true;
                }
            }
            return false;
        }
#endif
    }
}