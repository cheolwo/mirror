(function () {
    window.ssalddelDocumentOutput = {
        printHtml: function (title, html) {
            var printWindow = window.open("", "_blank");
            if (!printWindow) {
                throw new Error("인쇄 창을 열 수 없습니다.");
            }

            printWindow.document.open();
            printWindow.document.write(html);
            printWindow.document.close();
            printWindow.document.title = title || "Ssalddel document";
            printWindow.focus();
            setTimeout(function () {
                printWindow.print();
            }, 250);
        },
        downloadHtml: function (fileName, html) {
            var blob = new Blob([html], { type: "text/html;charset=utf-8" });
            var url = URL.createObjectURL(blob);
            var link = document.createElement("a");
            link.href = url;
            link.download = fileName || "ssalddel-document.html";
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        }
    };
})();
