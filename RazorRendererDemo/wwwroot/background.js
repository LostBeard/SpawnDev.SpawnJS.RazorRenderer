// Script loader works in window and worker scopes
//
// Synchronously fired events need to be captured by Javascript and
// held for .Net Wasm to pick up and handle once it loads.

// async function nuclearCleanup() {
//     // Clear all persistent dynamic rules
//     const dynamicRules = await chrome.declarativeNetRequest.getDynamicRules();
//     const dynamicIds = dynamicRules.map(r => r.id);
//     if (dynamicIds.length > 0) {
//         await chrome.declarativeNetRequest.updateDynamicRules({ removeRuleIds: dynamicIds });
//         console.log("Successfully purged ghost dynamic rules:", dynamicIds);
//     }

//     // Clear all in-memory session rules
//     const sessionRules = await chrome.declarativeNetRequest.getSessionRules();
//     const sessionIds = sessionRules.map(r => r.id);
//     if (sessionIds.length > 0) {
//         await chrome.declarativeNetRequest.updateSessionRules({ removeRuleIds: sessionIds });
//         console.log("Successfully purged session rules:", sessionIds);
//     }
// }
// nuclearCleanup();

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
// simple sleep function
const sleep = () => new Promise((res) => setTimeout(res, duration));
// Tracker for tab patching to prevent re-patch attempts after a failed patch
const cspTabs = {};
// Failed patch origin tracker to allow ignoring a site once a patch fails on it
const cspIgnore = [];
// handles a csp violation message from a content script.
// notifies the content script if the csp was patched and it should reload (reload == true) or if the csp cannot be patched (reload == false)
async function patchCSP(request, sender, sendResponse) {
    if (request.cspViolation) {
        const url = new URL(request.cspViolation.documentURI);
        // if this origin has been added to the ginroe lsit (likely due to patch failure) we ignore it now
        if (cspIgnore.includes(url.origin)) {
            sendResponse({ reload: false, reason: 'patch_failed_origin' });
            return;
        }
        const originalPolicy = request.cspViolation.originalPolicy;
        // if it is using nonce we do not try patching (at this time) as nonce sites have strict csp that is hard to adjust using declarative rules
        const hasNonce = originalPolicy.includes('nonce-');
        // ignore nonce csp
        if (hasNonce) {
            sendResponse({ reload: false, reason: 'nonce' });
            return;
        }
        // repeat failure test
        var cspTab = cspTabs[sender.tab.id];
        if (!cspTab || cspTab.url !== url) {
            // save the url, and unpatched originalPolicy
            cspTab = { url: url, originalPolicy, patchRuleId: null };
            cspTabs[sender.tab.id] = cspTab;
        } else {
            // same url as last time
            // already patched. if that did not work, then patching again and reloading will not help.
            // remove the patch that did not work
            if (cspTab.patchRuleId) {
                var patchRuleId = cspTab.patchRuleId;
                cspTab.patchRuleId = null;
                await chrome.declarativeNetRequest.updateSessionRules({ removeRuleIds: [patchRuleId] });
            }
            sendResponse({ reload: false, reason: 'patch_failed' });
            cspIgnore.push(url.origin);
            return;
        }
        // separate paths on the same domain may have different csp rules so we scope the rule to the page
        // the query string and hash shouldn't have any effect on csp rules so they are ignored in the scope
        const pageUrl = url.origin + url.pathname;
        const pageUrlEscaped = escapeRegExp(pageUrl);
        const extensionOrigin = chrome.runtime.getURL('').split('/').slice(0, 3).join('/');
        const scriptRegex = /(script-src|default-src)\s+/gi;
        if (scriptRegex.test(originalPolicy)) {
            updatedPolicy = originalPolicy.replace(scriptRegex, `$1 'wasm-unsafe-eval' ${extensionOrigin} 'self' `);
        } else {
            updatedPolicy = originalPolicy + `; script-src 'self' 'wasm-unsafe-eval' ${extensionOrigin}`;
        }
        const cspRule = {
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
                tabIds: [sender.tab.id],
                regexFilter: `^${pageUrlEscaped}(\\?.*)?(#.*)?$`,
                resourceTypes: ["main_frame", "sub_frame", "xmlhttprequest"]
            }
        };
        cspTab.patchRuleId = cspRule.id;
        // save rule
        await chrome.declarativeNetRequest.updateSessionRules({ addRules: [cspRule] });
        // the content script can reload the page (could be done here also)
        sendResponse({ reload: true });
    }
}
// remove tab rules on close
chrome.tabs.onRemoved.addListener(async (tabId) => {
    // delete the repeat patch tracker for this tab
    delete cspTabs[tabId];
    // remove tab specific rules
    const currentRules = await chrome.declarativeNetRequest.getSessionRules();
    // Find and isolate any rules generated by your library for the closed tab
    const rulesToRemove = currentRules
        .filter(rule => rule.condition.tabIds && rule.condition.tabIds.includes(tabId))
        .map(rule => rule.id);

    if (rulesToRemove.length > 0) {
        await chrome.declarativeNetRequest.updateSessionRules({
            removeRules: rulesToRemove
        });
        console.log(`[Library] Flushed rules for closed tab: ${tabId}`);
    }
});
chrome.runtime.onMessage.addListener(function (request, sender, sendResponse) {
    if (request.cspViolation) {
        // content script failed to load .Net Wasm due to csp rules
        patchCSP(request, sender, sendResponse);
        // return true to allow async response
        return true;
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
