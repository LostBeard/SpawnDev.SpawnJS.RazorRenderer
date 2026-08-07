(async () => {
    // Get the absolute path of the module inside the extension folder
    const moduleUrl = chrome.runtime.getURL('wwwroot/main.module.js');

    // Dynamically import the entry module
    await import(moduleUrl);
})();