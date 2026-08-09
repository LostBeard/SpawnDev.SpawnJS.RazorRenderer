// content.js
(async function () {
    // If the site's CSP rules block WebAssembly loading this handler will notify the
    // extension's background script and it can add a rule to enable it and reload the tab.
    // To date, the only site I have found that this was needed for was github.com
    // Documented here in case the issue is stumlbed on by others elsewhere
    const verbose = 0;  // 0 none, 1 errors only, 2 errors and info
    function onSecurityPolicyViolation(e) {
        // chrome - e.blockedURI === "wasm-eval"
        if (e.disposition === 'report') return;
        if ((e.blockedURI === "wasm-eval" || e.blockedURI === "wasm-unsafe-eval")
            && e.violatedDirective === "script-src"
            && e.originalPolicy.indexOf('wasm-unsafe-eval') === -1) {
            document.removeEventListener('securitypolicyviolation', onSecurityPolicyViolation);
            var cspViolation = {
                documentURI: e.documentURI,                 // document.location.href
                originalPolicy: e.originalPolicy,           // csp header value
                disposition: e.disposition,                 // disposition
            };
            if (verbose >= 2) console.log('onSecurityPolicyViolation', cspViolation);
            chrome.runtime.sendMessage({ cspViolation }, (resp) => {
                if (verbose >= 2) console.log('onSecurityPolicyViolation resp', resp);
                if (resp.reload) {
                    if (verbose >= 2) console.log('[RazorRendererDemo] onSecurityPolicyViolation will reload page to attempt 1 more time.');
                    location.reload();
                } else {
                    if (verbose >= 2) console.log('[RazorRendererDemo] onSecurityPolicyViolation will not reload page. Accepting load failure.', resp.reason);
                }
            });
        }
    }
    document.addEventListener('securitypolicyviolation', onSecurityPolicyViolation);
    const origin = new URL(location.href).origin;
    if (verbose >= 2) console.log('[RazorRendererDemo] Loading .Net Wasm extension in content scope.', origin, `TopMost: ${window.self === window.top}`);
    try {
        // Load .Net app
        await import(chrome.runtime.getURL('app/main.module.js'));
        // will not reach here until after the .Net Wasm app exits.
    } catch (ex) {
        if (verbose >= 1) console.log('[RazorRendererDemo] Failed to load .Net Wasm extension', origin, `TopMost: ${window.self === window.top}`);
    }
})();
