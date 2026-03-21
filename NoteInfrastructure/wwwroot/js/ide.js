document.addEventListener("DOMContentLoaded", () => {
    let editor;
    let currentFileId = null;
    let currentVersionId = null;

    // --- 1. Ініціалізація Monaco Editor ---
    require.config({ paths: { 'vs': 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.44.0/min/vs' } });
    require(['vs/editor/editor.main'], function () {
        editor = monaco.editor.create(document.getElementById('monaco-editor-container'), {
            value: "// Виберіть файл у провіднику зліва для початку роботи\n",
            language: 'csharp',
            theme: 'vs-dark',
            automaticLayout: true
        });
    });

    // --- 2. Логіка виділення та кнопок ---
    let selectedItem = null;
    let selectedFolderId = null;

    const btnNewFile = document.getElementById('btn-new-file');
    const btnNewFolder = document.getElementById('btn-new-folder');
    const btnDelete = document.getElementById('btn-delete-item');
    const treeContainer = document.getElementById('explorer-tree');

    function updateToolbarState() {
        if (!selectedItem) {
            btnNewFile.disabled = true;
            btnNewFolder.disabled = false;
            btnDelete.disabled = true;
            selectedFolderId = null;
        } else {
            const type = selectedItem.dataset.type;
            btnDelete.disabled = false;

            if (type === 'folder') {
                btnNewFile.disabled = false;
                btnNewFolder.disabled = false;
                selectedFolderId = parseInt(selectedItem.dataset.id);
            } else if (type === 'file') {
                btnNewFile.disabled = true;
                btnNewFolder.disabled = false;
                selectedFolderId = parseInt(selectedItem.dataset.parent) || null;
            }
        }
    }

    treeContainer.addEventListener('click', async (e) => {
        const item = e.target.closest('.tree-item');
        if (item) {
            if (selectedItem) selectedItem.classList.remove('selected');
            selectedItem = item;
            selectedItem.classList.add('selected');
            updateToolbarState();

            // Якщо клікнули на файл — завантажуємо його
            if (item.dataset.type === 'file') {
                await loadFileDetails(item.dataset.id);
            }
        } else {
            if (selectedItem) selectedItem.classList.remove('selected');
            selectedItem = null;
            updateToolbarState();
        }
    });

    // --- Завантаження коду файлу ---
    async function loadFileDetails(id) {
        currentFileId = id;
        document.getElementById('current-file-name').innerText = selectedItem.querySelector('span').innerText;
        editor.setValue("// Завантаження...");

        try {
            const response = await fetch(`/api/ide/files/${id}`);
            if (response.ok) {
                const data = await response.json();
                editor.setValue(data.currentContent || "");
                currentVersionId = data.currentVersionId;
                document.getElementById('file-description').value = data.description || "";

                // Рендер історії версій
                renderVersionsList(data.versions);
            }
        } catch (err) {
            console.error("Помилка завантаження файлу", err);
        }
    }

    function renderVersionsList(versions) {
        const list = document.getElementById('versions-list');
        list.innerHTML = "";
        versions.forEach(v => {
            const date = new Date(v.createdat).toLocaleString('uk-UA');
            list.innerHTML += `
                <div class="version-item ${v.id === currentVersionId ? 'active' : ''}">
                    <div class="version-info">
                        <strong>v${v.versionnumber} ${v.id === currentVersionId ? '(Поточна)' : ''}</strong>
                        <small>${date}</small>
                    </div>
                </div>`;
        });
    }

    // --- 3. Перейменування (Подвійний клік) ---
    treeContainer.addEventListener('dblclick', (e) => {
        const item = e.target.closest('.tree-item');
        if (!item) return;

        const span = item.querySelector('span');
        const oldName = span.innerText;
        const input = document.createElement('input');
        input.type = 'text';
        input.className = 'rename-input';
        input.value = oldName;

        span.replaceWith(input);
        input.focus();

        const saveRename = async () => {
            const newName = input.value.trim() || oldName;
            const newSpan = document.createElement('span');
            newSpan.innerText = newName;
            input.replaceWith(newSpan);

            if (newName !== oldName) {
                await fetch('/api/ide/rename', {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ id: item.dataset.id, type: item.dataset.type, name: newName })
                });
            }
        };

        input.addEventListener('blur', saveRename);
        input.addEventListener('keypress', (e) => { if (e.key === 'Enter') input.blur(); });
    });

    // --- 4 & 5. Згортання та Маштабування (залишено без змін) ---
    // (Тут твій попередній код для setupResizer та btnToggleBottom / btnToggleRight)
    function setupResizer(resizerId, panelId, isHorizontal, reverse = false) {
        const resizer = document.getElementById(resizerId);
        const panel = document.getElementById(panelId);
        if (!resizer || !panel) return;
        let startPos, startSize;
        resizer.addEventListener('mousedown', function (e) {
            startPos = isHorizontal ? e.clientY : e.clientX;
            startSize = isHorizontal ? panel.getBoundingClientRect().height : panel.getBoundingClientRect().width;
            document.documentElement.addEventListener('mousemove', doDrag, false);
            document.documentElement.addEventListener('mouseup', stopDrag, false);
            resizer.classList.add('resizing');
            e.preventDefault();
        });
        function doDrag(e) {
            if (isHorizontal) panel.style.height = `${reverse ? startSize + (startPos - e.clientY) : startSize + (e.clientY - startPos)}px`;
            else panel.style.width = `${reverse ? startSize + (startPos - e.clientX) : startSize + (e.clientX - startPos)}px`;
        }
        function stopDrag() {
            document.documentElement.removeEventListener('mousemove', doDrag, false);
            document.documentElement.removeEventListener('mouseup', stopDrag, false);
            resizer.classList.remove('resizing');
        }
    }
    setupResizer('resizer-left', 'explorer-panel', false);
    setupResizer('resizer-right', 'versions-panel', false, true);
    setupResizer('resizer-bottom', 'meta-panel', true, true);

    document.getElementById('btn-toggle-right-panel').addEventListener('click', () => { document.getElementById('versions-panel').style.display = 'flex'; document.getElementById('resizer-right').style.display = 'block'; });
    document.getElementById('btn-close-right-panel').addEventListener('click', () => { document.getElementById('versions-panel').style.display = 'none'; document.getElementById('resizer-right').style.display = 'none'; });

    // --- 6. Прив'язка до C# Backend (API Calls) ---
    document.getElementById('btn-new-folder').addEventListener('click', async () => {
        const name = prompt("Введіть ім'я папки:");
        if (!name) return;

        const response = await fetch('/api/ide/folders', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name: name, parentId: selectedFolderId })
        });

        if (response.ok) {
            const data = await response.json();
            // Додати візуально в дерево (спрощений варіант, краще зробити ре-рендер всього дерева)
            treeContainer.innerHTML += `<div class="tree-item folder-item" data-type="folder" data-id="${data.id}"><i class="bi bi-folder"></i> <span>${data.name}</span></div>`;
        }
    });

    document.getElementById('btn-new-file').addEventListener('click', async () => {
        if (!selectedFolderId) return alert('Оберіть папку для збереження файлу!');
        const name = prompt("Введіть ім'я файлу (напр. NewFile.cs):");
        if (!name) return;

        const response = await fetch('/api/ide/files', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name: name, folderId: selectedFolderId })
        });

        if (response.ok) {
            const data = await response.json();
            treeContainer.innerHTML += `<div class="tree-item file-item" data-type="file" data-id="${data.id}" data-parent="${selectedFolderId}"><i class="bi bi-file-code"></i> <span>${data.name}</span></div>`;
        }
    });

    document.getElementById('btn-save-version').addEventListener('click', async () => {
        if (!currentVersionId) return alert('Не обрано файл або версію!');
        const code = editor.getValue();

        await fetch(`/api/ide/versions/${currentVersionId}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ content: code })
        });
        // Візуальне сповіщення можна додати через Toast
    });

    document.getElementById('btn-new-version').addEventListener('click', async () => {
        if (!currentFileId) return alert('Оберіть файл!');
        const code = editor.getValue();

        const response = await fetch('/api/ide/versions', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ fileId: currentFileId, content: code })
        });

        if (response.ok) {
            await loadFileDetails(currentFileId); // Перезавантажуємо файл, щоб оновити історію версій
        }
    });
});