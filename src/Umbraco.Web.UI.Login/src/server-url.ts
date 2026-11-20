// Login requests use the origin serving this page and its configured application path.
export const loginServerUrl = window.location.origin + (document.documentElement.dataset.applicationPath ?? '');
