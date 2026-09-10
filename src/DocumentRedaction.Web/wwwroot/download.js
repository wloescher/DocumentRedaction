// Streams a redacted document from the Blazor circuit to the browser as a file download.
// Nothing is stored server-side, so the bytes travel over the circuit once and are released.
window.documentRedaction = {
  download: async function (fileName, contentType, streamReference) {
    const buffer = await streamReference.arrayBuffer();
    const blob = new Blob([buffer], { type: contentType });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
  }
};
