﻿using HarmonyLib;
using UnityEngine;
using PeroTools2.Resources;
using System;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Collections.Generic;
using Assets.Scripts.PeroTools.GeneralLocalization;
using MelonLoader;
using System.Reflection;
using UnhollowerBaseLib;
using System.Runtime.InteropServices;

namespace CustomAlbums.Patch
{
    class AssetPatch
    {
        private static readonly Logger Log = new Logger("AssetPatch");

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr LoadFromNameDelegate(
            IntPtr instance,
            IntPtr assetName,
            IntPtr nativeMethodInfo
        );
        private static LoadFromNameDelegate OriginalLoadFromName;

        public static Dictionary<string, UnityEngine.Object> LoadedAssets = new Dictionary<string, UnityEngine.Object>();
        public static string[] AssetSuffixes = new string[] {
                "_demo",
                "_music",
                "_cover",
                "_map1",
                "_map2",
                "_map3",
                "_map4"
            };
        public static unsafe void DoPatching()
        {
            var type = typeof(ResourcesManager).GetNestedNonPublicType("MethodInfoStoreGeneric_LoadFromName_Public_T_String_0`1").MakeGenericType(typeof(TextAsset));
            var originalMethod = *(IntPtr*)(IntPtr)(type.GetField("Pointer", BindingFlags.NonPublic | BindingFlags.Static).GetValue(type));
            var methodPatchPtr = AccessTools.Method(typeof(AssetPatch), nameof(AssetPatch.LoadFromName)).MethodHandle.GetFunctionPointer();

            MelonUtils.NativeHookAttach((IntPtr)(&originalMethod), methodPatchPtr);
            OriginalLoadFromName = Marshal.GetDelegateForFunctionPointer<LoadFromNameDelegate>(originalMethod);
            Log.Debug($"Patched LoadFromName");
        }

        public static IntPtr LoadFromName(IntPtr instance, IntPtr assetName, IntPtr nativeMethodInfo)
        {
            var _assetName = IL2CPP.Il2CppStringToManaged(assetName);
            //Log.Debug($"assetName {_assetName}");

            // Cached asset
            if (LoadedAssets.TryGetValue(_assetName, out var asset))
            {
                Log.Debug($"Use cache: {_assetName}");
                return asset.Pointer;
            }

            var assetPtr = OriginalLoadFromName(instance, assetName, nativeMethodInfo);

            if (_assetName == "LocalizationSettings")
                return assetPtr;
            string lang = Assets.Scripts.PeroTools.Commons.SingletonScriptableObject<LocalizationSettings>.instance.GetActiveOption("Language");

            UnityEngine.Object newAsset = null;

            // Json
            if (_assetName == "albums")
            {
                var textAsset = new TextAsset(assetPtr);
                var jArray = textAsset.text.JsonDeserialize<JArray>();
                jArray.Add(JObject.FromObject(new
                {
                    uid = AlbumManager.MusicPackge,
                    title = "Custom Albums",
                    prefabsName = $"AlbumDisco{AlbumManager.Uid}",
                    price = "¥25.00",
                    jsonName = AlbumManager.JsonName,
                    needPurchase = false,
                    free = true,
                }));
                newAsset = CreateTextAsset(_assetName, jArray.JsonSerialize());
            }
            else if (_assetName == AlbumManager.JsonName)
            {
                var jArray = new JArray();
                foreach (var keyValue in AlbumManager.LoadedAlbums)
                {
                    var key = keyValue.Key;
                    var album = keyValue.Value;
                    var info = album.Info;
                    var jObject = new JObject();
                    jObject.Add("uid", $"{AlbumManager.Uid}-{album.Index}");
                    jObject.Add("name", info.GetName());
                    jObject.Add("author", info.GetAuthor());
                    jObject.Add("bpm", info.bpm);
                    jObject.Add("music", $"{key}_music");
                    jObject.Add("demo", $"{key}_demo");
                    jObject.Add("cover", $"{key}_cover");
                    jObject.Add("noteJson", $"{key}_map");
                    jObject.Add("scene", info.scene);
                    jObject.Add("unlockLevel", string.IsNullOrEmpty(info.unlockLevel) ? "0" : info.unlockLevel);
                    if (!string.IsNullOrEmpty(info.levelDesigner))
                        jObject.Add("levelDesigner", info.levelDesigner);
                    if (!string.IsNullOrEmpty(info.levelDesigner1))
                        jObject.Add("levelDesigner1", info.levelDesigner1);
                    if (!string.IsNullOrEmpty(info.levelDesigner2))
                        jObject.Add("levelDesigner2", info.levelDesigner2);
                    if (!string.IsNullOrEmpty(info.levelDesigner3))
                        jObject.Add("levelDesigner3", info.levelDesigner3);
                    if (!string.IsNullOrEmpty(info.levelDesigner4))
                        jObject.Add("levelDesigner4", info.levelDesigner4);
                    if (!string.IsNullOrEmpty(info.difficulty1))
                        jObject.Add("difficulty1", info.difficulty1);
                    if (!string.IsNullOrEmpty(info.difficulty2))
                        jObject.Add("difficulty2", info.difficulty2);
                    if (!string.IsNullOrEmpty(info.difficulty3))
                        jObject.Add("difficulty3", info.difficulty3);
                    if (!string.IsNullOrEmpty(info.difficulty4))
                        jObject.Add("difficulty4", info.difficulty4);
                    jArray.Add(jObject);
                }
                newAsset = CreateTextAsset(_assetName, jArray.JsonSerialize());
            }
            else if (_assetName == $"albums_{lang}")
            {
                var textAsset = new TextAsset(assetPtr);
                var jArray = textAsset.text.JsonDeserialize<JArray>();
                jArray.Add(JObject.FromObject(new
                {
                    title = AlbumManager.Langs[lang],
                }));
                newAsset = CreateTextAsset(_assetName, jArray.JsonSerialize());
            }
            else if (_assetName == $"{AlbumManager.JsonName}_{lang}")
            {
                var jArray = new JArray();
                foreach (var keyValue in AlbumManager.LoadedAlbums)
                {
                    jArray.Add(JObject.FromObject(new
                    {
                        name = keyValue.Value.Info.GetName(lang),
                        author = keyValue.Value.Info.GetAuthor(lang),
                    }));
                }
                newAsset = CreateTextAsset(_assetName, jArray.JsonSerialize());
            }
            else if (_assetName == "defaultTag")
            {
                var textAsset = new TextAsset(assetPtr);
                var jArray = textAsset.text.JsonDeserialize<JArray>();
                // Replace Cute tag
                var music_tag = jArray.First(o => o.Value<int>("sort_key") == 8);
                music_tag["tag_name"] = JObject.FromObject(AlbumManager.Langs);
                music_tag["tag_picture"] = "https://mdmc.moe/cdn/melon.png";
                music_tag["pic_name"] = "";
                music_tag["music_list"] = JArray.FromObject(AlbumManager.GetAllUid());

                newAsset = CreateTextAsset(_assetName, jArray.JsonSerialize());
            }

            if (assetPtr == IntPtr.Zero)
            {
                // Try load custom asset
                if (_assetName.StartsWith("fs_") || _assetName.StartsWith("pkg_"))
                {
                    var suffix = AssetSuffixes.FirstOrDefault(s => _assetName.EndsWith(s));
                    if (!string.IsNullOrEmpty(suffix))
                    {
                        var albumKey = _assetName.RemoveFromEnd(suffix);
                        AlbumManager.LoadedAlbums.TryGetValue(albumKey, out var album);
                        switch (suffix)
                        {
                            case "_demo":
                                newAsset = album?.GetMusic("demo");
                                break;
                            case "_music":
                                newAsset = album?.GetMusic();
                                break;
                            case "_cover":
                                newAsset = album?.GetCover();
                                break;
                            default:
                                Log.Debug($"Unknown suffix: {suffix}");
                                break;
                        }
                    }
                }
            }

            // Add to cache
            if (newAsset != null)
            {
                Log.Debug($"Cached {_assetName}");
                LoadedAssets.Add(_assetName, newAsset);
                return newAsset.Pointer;
            }

            return assetPtr;
        }

        /// <summary>
        /// Create a new TextAsset
        /// </summary>
        /// <param name="name"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public static TextAsset CreateTextAsset(string name, string text)
        {
            var newAsset = new TextAsset(text);
            newAsset.name = name;
            return newAsset;
        }
    }
}
