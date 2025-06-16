
document.addEventListener("DOMContentLoaded", function () {
    const form = document.querySelector("form");
    const resultArea = document.querySelector("#messageResult");

    form.addEventListener("submit", function (e) {
        e.preventDefault();
        const formData = new FormData(form);
        const query = new URLSearchParams(formData).toString();

        fetch(`/project/index?${query}`, {
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            }
        })
        .then(res => res.text())
        .then(html => {
            const parser = new DOMParser();
            const doc = parser.parseFromString(html, "text/html");
            const newContent = doc.querySelector("#messageResult").innerHTML;
            resultArea.innerHTML = newContent;
        });
    });
});


document.addEventListener("DOMContentLoaded", function () {
    const form = document.querySelector("form");

    // 動態設定 replyID
    document.body.addEventListener("click", function (e) {
        if (e.target.classList.contains("reply-btn")) {
            const replyID = e.target.dataset.id;
            const mainInput = document.querySelector("textarea[name='main']");
            const replyField = document.querySelector("input[name='replyID']");
            if (replyField) replyField.value = replyID;
            if (mainInput) mainInput.focus();
        }
    });

    // 刪除留言
    document.body.addEventListener("click", function (e) {
        if (e.target.classList.contains("delete-btn")) {
            const id = e.target.dataset.id;
            if (confirm("確定要刪除這則留言嗎？")) {
                fetch("/project/DeleteMessage", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/x-www-form-urlencoded"
                    },
                    body: "messageID=" + id
                }).then(() => location.reload());
            }
        }
    });
});


document.addEventListener("DOMContentLoaded", function () {
    const messageForm = document.querySelector("#messageForm");
    const resultArea = document.querySelector("#messageResult");

    if (messageForm) {
        messageForm.addEventListener("submit", function (e) {
            e.preventDefault();
            const formData = new FormData(messageForm);
            fetch("/project/SubmitMessage", {
                method: "POST",
                body: new URLSearchParams(formData)
            }).then(res => res.ok && reloadMessages());
        });
    }

    function reloadMessages() {
        const form = document.querySelector("form");
        if (!form) return;
        const formData = new FormData(form);
        const query = new URLSearchParams(formData).toString();

        fetch(`/project/index?${query}`, {
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            }
        })
        .then(res => res.text())
        .then(html => {
            const parser = new DOMParser();
            const doc = parser.parseFromString(html, "text/html");
            resultArea.innerHTML = doc.querySelector("#messageResult").innerHTML;
            messageForm.reset();
        });
    }
});
