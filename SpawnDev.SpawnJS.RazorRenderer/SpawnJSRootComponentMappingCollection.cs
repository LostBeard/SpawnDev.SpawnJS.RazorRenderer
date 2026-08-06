using Microsoft.AspNetCore.Components;
using SpawnDev.SpawnJS.JSObjects;
using System.Collections.ObjectModel;

namespace SpawnDev.SpawnJS.RazorRenderer
{
    /// <summary>
    /// Pre-registered Components that will be setup anbd ran when the app starts in a Window context
    /// </summary>
    public class SpawnJSRootComponentMappingCollection : Collection<SpawnJSRootComponentMapping>
    {
        /// <summary>
        /// Style sheet urls that will be used for all root components
        /// </summary>
        public List<string> StyleSheets { get; private set; } = new List<string>();
        /// <summary>
        /// Add style sheets for all root components
        /// </summary>
        /// <param name="styleSheetUrls"></param>
        /// <returns></returns>
        public SpawnJSRootComponentMappingCollection AddSharedStyleSheet(params string[] styleSheetUrls)
        {
            StyleSheets.AddRange(styleSheetUrls);
            return this;
        }
        #region Target-host
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(Element host) where TComponent : IComponent
        {
            ArgumentNullException.ThrowIfNull(host);
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), host, null));
        }
        SpawnJSRootComponentMapping AddReturn(SpawnJSRootComponentMapping item)
        {
            Add(item);
            return item;
        }
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(Element host, ParameterView parameters) where TComponent : IComponent
        {
            ArgumentNullException.ThrowIfNull(host);
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), host, parameters));
        }
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(Element host, Dictionary<string, object?> parameters) where TComponent : IComponent
        {
            ArgumentNullException.ThrowIfNull(host);
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), host, parameters == null ? null : ParameterView.FromDictionary(parameters)));
        }
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(Element host, AttachShadowRootOptions shadowRootOptions) where TComponent : IComponent
        {
            ArgumentNullException.ThrowIfNull(host);
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), host, shadowRootOptions, null));
        }
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(Element host, AttachShadowRootOptions shadowRootOptions, ParameterView parameters) where TComponent : IComponent
        {
            ArgumentNullException.ThrowIfNull(host);
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), host, shadowRootOptions, parameters));
        }
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(Element host, AttachShadowRootOptions shadowRootOptions, Dictionary<string, object?> parameters) where TComponent : IComponent
        {
            ArgumentNullException.ThrowIfNull(host);
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), host, shadowRootOptions, parameters == null ? null : ParameterView.FromDictionary(parameters)));
        }
        #endregion
        #region Target-selector
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(string selector) where TComponent : IComponent
        {
            ArgumentNullException.ThrowIfNull(selector);
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), selector, null));
        }
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(string selector, ParameterView parameters) where TComponent : IComponent
        {
            ArgumentNullException.ThrowIfNull(selector);
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), selector, parameters));
        }
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(string selector, Dictionary<string, object?> parameters) where TComponent : IComponent
        {
            ArgumentNullException.ThrowIfNull(selector);
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), selector, parameters == null ? null : ParameterView.FromDictionary(parameters)));
        }
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(string selector, AttachShadowRootOptions shadowRootOptions) where TComponent : IComponent
        {
            ArgumentNullException.ThrowIfNull(selector);
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), selector, shadowRootOptions, null));
        }
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(string selector, AttachShadowRootOptions shadowRootOptions, ParameterView parameters) where TComponent : IComponent
        {
            ArgumentNullException.ThrowIfNull(selector);
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), selector, shadowRootOptions, parameters));
        }
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(string selector, AttachShadowRootOptions shadowRootOptions, Dictionary<string, object?> parameters) where TComponent : IComponent
        {
            ArgumentNullException.ThrowIfNull(selector);
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), selector, shadowRootOptions, parameters == null ? null : ParameterView.FromDictionary(parameters)));
        }
        #endregion
        #region Target-new
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>() where TComponent : IComponent
        {
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), null));
        }
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(ParameterView parameters) where TComponent : IComponent
        {
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), parameters));
        }
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(Dictionary<string, object?> parameters) where TComponent : IComponent
        {
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), parameters == null ? null : ParameterView.FromDictionary(parameters)));
        }
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(AttachShadowRootOptions shadowRootOptions) where TComponent : IComponent
        {
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), shadowRootOptions, null));
        }
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(AttachShadowRootOptions shadowRootOptions, ParameterView parameters) where TComponent : IComponent
        {
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), shadowRootOptions, parameters));
        }
        /// <summary>
        /// Adds a component mapping to the collection.
        /// </summary>
        public SpawnJSRootComponentMapping Add<TComponent>(AttachShadowRootOptions shadowRootOptions, Dictionary<string, object?> parameters) where TComponent : IComponent
        {
            return AddReturn(new SpawnJSRootComponentMapping(typeof(TComponent), shadowRootOptions, parameters == null ? null : ParameterView.FromDictionary(parameters)));
        }
        #endregion
        /// <summary>
        /// Adds a collection of items to this collection.
        /// </summary>
        /// <param name="items">The items to add.</param>
        public void AddRange(IEnumerable<SpawnJSRootComponentMapping> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            foreach (var item in items)
            {
                Add(item);
            }
        }
    }
}
