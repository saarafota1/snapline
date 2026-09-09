using System.Collections.Generic;
using UnityEngine;

namespace Snapline.Art
{
    /// <summary>
    /// Loads authored artwork out of <c>Resources/Snapline/</c>, and says so honestly when a file
    /// is not there.
    ///
    /// The game shipped with every sprite generated at runtime. Authored art is arriving screen by
    /// screen, so for a while both have to work at once: each call site asks for a file and falls
    /// back to the generator it was already using if the file is missing. That keeps the game
    /// runnable at every point during the changeover rather than only once the last PNG lands.
    ///
    /// Nothing here decides what a missing file should look like — the caller does, because only
    /// the caller knows what it was drawing before.
    /// </summary>
    public static class ArtLoader
    {
        private const string Root = "Snapline/";

        /// <summary>
        /// Cached per path, including misses. A miss is cached deliberately: without it every
        /// frame that rebuilds a panel re-runs a failing Resources.Load, which is not free.
        /// </summary>
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// The sprite at <paramref name="path"/> relative to <c>Resources/Snapline/</c>, or null.
        ///
        /// Both import shapes are accepted. A texture imported as a Sprite comes back directly; one
        /// left as a plain Texture2D — which is what an unconfigured PNG dropped into the project
        /// is — gets wrapped. That means new artwork works the moment it is copied in, whether or
        /// not its import settings have been touched yet.
        /// </summary>
        public static Sprite Sprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (Cache.TryGetValue(path, out Sprite cached)) return cached;

            Sprite sprite = Resources.Load<Sprite>(Root + path);
            if (sprite == null)
            {
                Texture2D tex = Resources.Load<Texture2D>(Root + path);
                if (tex != null)
                {
                    sprite = UnityEngine.Sprite.Create(
                        tex,
                        new Rect(0f, 0f, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f),
                        100f,
                        0,
                        SpriteMeshType.FullRect);
                    sprite.name = path;
                }
            }

            Cache[path] = sprite;
            return sprite;
        }

        /// <summary>True if authored art exists at this path. Cheap after the first call.</summary>
        public static bool Has(string path) => Sprite(path) != null;

        /// <summary>
        /// Drops the cache. Only the editor needs this — a domain reload already clears it in a
        /// player — but re-importing art while in play mode otherwise keeps showing the old sprite.
        /// </summary>
        public static void ClearCache() => Cache.Clear();
    }
}
