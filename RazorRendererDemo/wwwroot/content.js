// content.js
(async function () {
    // If the site's CSP rules block WebAssembly loading this handler will notify the 
    // extension's background script and it can add a rule to enable it and reload the tab.
    function onSecurityPolicyViolation(e) {
        // chrome - e.blockedURI === "wasm-eval"
        if ((e.blockedURI === "wasm-eval" || e.blockedURI === "wasm-unsafe-eval")
            && e.violatedDirective === "script-src"
            && e.originalPolicy.indexOf('wasm-unsafe-eval') === -1) {
            document.removeEventListener('securitypolicyviolation', onSecurityPolicyViolation);
            var cspViolation = {
                documentURI: e.documentURI,                 // document.location.href
                originalPolicy: e.originalPolicy            // csp header value
            };
            browser.runtime.sendMessage({ cspViolation });
        }
    }
    document.addEventListener('securitypolicyviolation', onSecurityPolicyViolation);
    // Load .Net app
    await import(chrome.runtime.getURL('app/main.module.js'));
})();
