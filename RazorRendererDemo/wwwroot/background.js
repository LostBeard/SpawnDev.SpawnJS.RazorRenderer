// Script loader works in window and worker scopes
//
// Synchronously fired events need to be captured by Javascript and
// held for .Net Wasm to pick up and handle once it loads.

async function getFreeSessionRuleId() {
    // Session rules and dynamic rules are SEPARATE id namespaces; this rule is added via updateSessionRules,
    // so the free-id search must look at the session rules, not the dynamic ones.
    var previousRules = await chrome.declarativeNetRequest.getSessionRules();
    var previousRuleIds = previousRules.map(rule => rule.id);
    if (previousRuleIds.length === chrome.declarativeNetRequest.MAX_NUMBER_OF_SESSION_RULES) {
        await chrome.declarativeNetRequest.updateSessionRules({ removeRules: previousRuleIds });
        previousRuleIds = [];
    }
    var availId = Math.floor(Math.random() * 1000000) + 1;
    while (previousRuleIds.indexOf(availId) !== -1) availId = Math.floor(Math.random() * 1000000) + 1;
    return availId;
}
// https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Regular_Expressions#escaping
function escapeRegExp(string) {
    return string.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"); // $& means the whole matched string
}
function sleep(duration) {
    return new Promise((res) => setTimeout(res, duration));
}
// some sites have csp ruels that do not allow for WebAssembly to load
// if the content.js script detects the script causign a csp rule exception it will
// message this script to modify the csp rule to allow for wasm-unsafe-eval
async function patchCSP(request, sender) {
    if (request.cspViolation) {
        var originalPolicy = request.cspViolation.originalPolicy;
        var updatedPolicy = originalPolicy;
        if (originalPolicy.indexOf('wasm-unsafe-eval') === -1) {
            updatedPolicy = originalPolicy.replace('script-src ', "script-src 'wasm-unsafe-eval' ");
        } else {
            // rule already has 'wasm-unsafe-eval'
            // if this happens, there is another problem
            return;
        }
        var url = new URL(request.cspViolation.documentURI);
        // separate paths on the same domain may have different csp rules
        // the query string and hash shouldn't have any effect on csp rules 
        var pageUrl = url.origin + url.pathname;
        var pageUrlEscaped = escapeRegExp(pageUrl);
        var cspRule = {
            id: await getFreeSessionRuleId(),
            action: {
                type: 'modifyHeaders',
                responseHeaders: [
                    {
                        header: 'content-security-policy',
                        operation: 'set',
                        value: updatedPolicy,
                    }
                ]
            },
            condition: {
                regexFilter: `^${pageUrlEscaped}(\\?.*)?(#.*)?$`,
                resourceTypes: ["main_frame", "sub_frame", "xmlhttprequest"]
            }
        };
        // save rule
        await chrome.declarativeNetRequest.updateSessionRules({
            addRules: [cspRule]
        });
        // reload tab so the new rule can take effect
        chrome.tabs.reload(sender.tab.id);
    }
}
chrome.runtime.onMessage.addListener(function (request, sender, sendResponse) {
    if (request.cspViolation) {
        patchCSP(request, sender);
    }
});

(async function () {
    async function loadScript(src) {
        src = chrome.runtime.getURL(src);
        if (typeof globalThis.importScripts === 'function') {
            // Chrome runs background scripts in ServiceWorkers scope
            globalThis.importScripts(src);
        } else if (globalThis.document) {
            // Firefox runs background scripts in (hidden) Window scope
            const script = document.createElement('script');
            const loadTask = new Promise((onload, onerror) => Object.assign(script, { onload, onerror, src }));
            (document.head || document.documentElement).append(script);
            await loadTask;
        }
    }
    // Load anything that needs to load before .Net Wasm
    // 
    // Load .Net Wasm app
    await loadScript('app/main.classic.js');
})();
