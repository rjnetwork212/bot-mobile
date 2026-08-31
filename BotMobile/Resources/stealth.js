// Stealth evasions - injected via EvaluateExpressionOnNewDocumentAsync before page scripts.
(() => {
  const fakeToString = (fn, name) => {
    const src = `function ${name}() { [native code] }`;
    try { Object.defineProperty(fn, 'toString', { value: () => src, writable: true }); } catch (e) {}
    return fn;
  };

  // navigator.webdriver
  try {
    if (navigator.webdriver !== undefined) {
      Object.defineProperty(Navigator.prototype, 'webdriver', {
        get: () => undefined,
        configurable: true,
      });
    }
  } catch (e) {}

  // window.chrome runtime object
  try {
    if (!window.chrome) {
      window.chrome = {};
    }
    if (!window.chrome.runtime) {
      window.chrome.runtime = {
        connect: fakeToString(function () {}, 'connect'),
        sendMessage: fakeToString(function () {}, 'sendMessage'),
      };
    }
  } catch (e) {}

  // plugins + mimeTypes (headless/CDP reports zero)
  try {
    Object.defineProperty(navigator, 'plugins', {
      get: () => {
        const arr = [
          { name: 'Chrome PDF Viewer', filename: 'internal-pdf-viewer', description: 'Portable Document Format' },
          { name: 'Chrome PDF Viewer', filename: 'internal-pdf-viewer', description: '' },
          { name: 'Chromium PDF Viewer', filename: 'internal-pdf-viewer', description: '' },
          { name: 'Microsoft Edge PDF Viewer', filename: 'internal-pdf-viewer', description: '' },
          { name: 'WebKit built-in PDF', filename: 'internal-pdf-viewer', description: '' },
        ];
        arr.item = fakeToString(function (i) { return this[i] || null; }, 'item');
        arr.namedItem = fakeToString(function (n) { return arr.find(p => p.name === n) || null; }, 'namedItem');
        arr.refresh = fakeToString(function () {}, 'refresh');
        return arr;
      },
      configurable: true,
    });
    Object.defineProperty(navigator, 'mimeTypes', {
      get: () => {
        const arr = [
          { type: 'application/pdf', suffixes: 'pdf', description: 'Portable Document Format' },
        ];
        arr.item = fakeToString(function (i) { return this[i] || null; }, 'item');
        arr.namedItem = fakeToString(function (n) { return arr.find(m => m.type === n) || null; }, 'namedItem');
        return arr;
      },
      configurable: true,
    });
  } catch (e) {}

  // WebGL vendor/renderer
  try {
    const origGetParameter = WebGLRenderingContext.prototype.getParameter;
    WebGLRenderingContext.prototype.getParameter = fakeToString(function (param) {
      if (param === 37445) return 'Qualcomm';
      if (param === 37446) return 'Adreno (TM) 740';
      return origGetParameter.call(this, param);
    }, 'getParameter');
  } catch (e) {}

  // permissions query: notifications should be prompt, not denied (headless tell)
  try {
    const origQuery = window.navigator.permissions.query;
    window.navigator.permissions.query = fakeToString(function (param) {
      if (param && param.name === 'notifications') {
        return Promise.resolve({ state: Notification.permission, onchange: null });
      }
      return origQuery.call(this, param);
    }, 'query');
  } catch (e) {}

  // canvas noise
  try {
    const origToDataURL = HTMLCanvasElement.prototype.toDataURL;
    HTMLCanvasElement.prototype.toDataURL = fakeToString(function (...args) {
      try {
        const ctx = this.getContext('2d');
        if (ctx) {
          const shift = { r: 1, g: -1, b: 1 };
          for (const [k, v] of Object.entries(shift)) {
            const comp = { r: 'red', g: 'green', b: 'blue' }[k];
            const data = ctx.getImageData(0, 0, 1, 1);
            data.data['rgb'.indexOf(k)] = data.data['rgb'.indexOf(k)] + v;
            ctx.fillStyle = `rgb(${data.data[0]},${data.data[1]},${data.data[2]})`;
            ctx.fillRect(0, 0, 1, 1);
          }
        }
      } catch (e) {}
      return origToDataURL.apply(this, args);
    }, 'toDataURL');
  } catch (e) {}

  // hardwareConcurrency + deviceMemory consistent with mid-range Android
  try {
    Object.defineProperty(navigator, 'hardwareConcurrency', { get: () => 8, configurable: true });
    Object.defineProperty(navigator, 'deviceMemory', { get: () => 8, configurable: true });
  } catch (e) {}
})();
