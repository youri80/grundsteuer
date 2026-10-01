// PortalDownload – kleiner JS-Helfer für den PDF-Export.
// Blazor Server kann eine erzeugte Datei nicht selbst "zurückschicken" (der Byte-Stream liegt
// serverseitig); deshalb wird der Inhalt als Base64 übergeben und im Browser als Datei abgelegt.
window.portalDownload = {
    pdf: function (dateiname, base64) {
        const binaer = atob(base64);
        const bytes = new Uint8Array(binaer.length);
        for (let i = 0; i < binaer.length; i++) {
            bytes[i] = binaer.charCodeAt(i);
        }

        const blob = new Blob([bytes], { type: 'application/pdf' });
        const url = URL.createObjectURL(blob);

        const link = document.createElement('a');
        link.href = url;
        link.download = dateiname;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);

        // Erst nach dem Klick freigeben, sonst bricht der Download in einigen Browsern ab.
        setTimeout(() => URL.revokeObjectURL(url), 10000);
    }
};
