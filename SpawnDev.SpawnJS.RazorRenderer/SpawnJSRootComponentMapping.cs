using Microsoft.AspNetCore.Components;
using SpawnDev.SpawnJS.JSObjects;

namespace SpawnDev.SpawnJS.RazorRenderer
{
    /// <summary>
    /// SpawnJSRootComponentMapping stores renering info for RazorRenderer root componentsthat wil be rendered when started ina Window global scope
    /// </summary>
    public class SpawnJSRootComponentMapping
    {
        /// <summary>
        /// The component id will be set once rendered
        /// </summary>
        public int ComponentId { get; internal set; }
        /// <summary>
        /// IComponent type to render
        /// </summary>
        public Type ComponentType { get; private set; }
        /// <summary>
        /// The existing Element to render to.<br/>
        /// </summary>
        public Element? Host { get; internal set; }
        /// <summary>
        /// The existing Element to render to.<br/>
        /// </summary>
        public ShadowRoot? ShadowRoot { get; internal set; }
        /// <summary>
        /// The selector to the target host to render to
        /// </summary>
        public string? Selector { get; private set; }
        /// <summary>
        /// If set, this style string will be applied to the target host
        /// </summary>
        public string HostStyle { get; private set; } = "";
        /// <summary>
        /// Called when the host is resolved. Can be used to modify the host before it is used, such as styling.
        /// </summary>
        public Func<SpawnJSRootComponentMapping, Task>? ConfigureHostCallback { get; private set; }
        /// <summary>
        /// Root component parameters
        /// </summary>
        public ParameterView? Parameters { get; private set; }
        /// <summary>
        /// Optional shadow root options specified if the renderer should render to the shadow root of the target
        /// </summary>
        public AttachShadowRootOptions? ShadowRootOptions { get; private set; }
        /// <summary>
        /// New instance
        /// </summary>
        public SpawnJSRootComponentMapping(Type componentType, string? selector, AttachShadowRootOptions? shadowRootOptions, ParameterView? parameters)
        {
            ComponentType = componentType;
            Selector = selector;
            ShadowRootOptions = shadowRootOptions;
            Parameters = parameters;
        }
        /// <summary>
        /// If set, this style string will be applied to the target host
        /// </summary>
        /// <param name="hostStyle"></param>
        /// <returns>this instance for chaining</returns>
        public SpawnJSRootComponentMapping SetHostStyle(string hostStyle)
        {
            HostStyle = hostStyle;
            return this;
        }
        /// <summary>
        /// If set, configureHostCallback will be called once the host has been created or found, and before it is rendered into.
        /// </summary>
        /// <param name="configureHostCallback"></param>
        /// <returns>this instance for chaining</returns>
        public SpawnJSRootComponentMapping ConfigureHost(Func<SpawnJSRootComponentMapping, Task> configureHostCallback)
        {
            ConfigureHostCallback = configureHostCallback;
            return this;
        }
        /// <summary>
        /// New instance
        /// </summary>
        public SpawnJSRootComponentMapping(Type componentType, string? selector, ParameterView? parameters)
        {
            ComponentType = componentType;
            Selector = selector;
            Parameters = parameters;
        }
        /// <summary>
        /// New instance
        /// </summary>
        public SpawnJSRootComponentMapping(Type componentType, Element? host, AttachShadowRootOptions? shadowRootOptions, ParameterView? parameters)
        {
            ComponentType = componentType;
            Host = host;
            ShadowRootOptions = shadowRootOptions;
            Parameters = parameters;
        }
        /// <summary>
        /// New instance
        /// </summary>
        public SpawnJSRootComponentMapping(Type componentType, Element? host, ParameterView? parameters)
        {
            ComponentType = componentType;
            Host = host;
            Parameters = parameters;
        }
        /// <summary>
        /// New instance
        /// </summary>
        public SpawnJSRootComponentMapping(Type componentType, AttachShadowRootOptions? shadowRootOptions, ParameterView? parameters)
        {
            ComponentType = componentType;
            ShadowRootOptions = shadowRootOptions;
            Parameters = parameters;
        }
        /// <summary>
        /// New instance
        /// </summary>
        public SpawnJSRootComponentMapping(Type componentType, ParameterView? parameters)
        {
            ComponentType = componentType;
            Parameters = parameters;
        }
    }
}
