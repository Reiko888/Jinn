using BepInEx.Configuration;
using Dusk;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace Jinn
{
        internal class JinnContentHandler : ContentHandler<JinnContentHandler>
        {
            internal JinnAssets? jinnAssets;
            internal GramophoneAssets? gramophoneAssets;
            internal RapierAssets? rapierAssets;


            public class JinnAssets(DuskMod mod, string filePath) : AssetBundleLoader<JinnAssets>(mod, filePath) {
            [LoadFromBundle("Jinn.prefab")]
            public GameObject Jinn { get; private set; } = null!;
        }
            public class GramophoneAssets(DuskMod mod, string filePath) : AssetBundleLoader<GramophoneAssets>(mod, filePath) { }
            public class RapierAssets(DuskMod mod, string filePath) : AssetBundleLoader<RapierAssets>(mod, filePath) { }


            public JinnContentHandler(DuskMod mod) : base(mod)
            {
                RegisterContent("jinn", out jinnAssets);
                RegisterContent("gramophone", out gramophoneAssets);
                RegisterContent("rapier", out rapierAssets);
            }
        }
}
