document.addEventListener("DOMContentLoaded", () => {
    // --- 1. Ініціалізація Monaco Editor ---
    let editor;
    require.config({ paths: { 'vs': 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.44.0/min/vs' } });
    require(['vs/editor/editor.main'], function () {
        editor = monaco.editor.create(document.getElementById('monaco-editor-container'), {
            value: "// Виберіть файл у провіднику зліва для початку роботи\n",
            language: 'csharp',
            theme: 'vs-dark',
            automaticLayout: true // Дуже важливо для правильного ресайзу панелей
        });
    });

    // --- 2. Логіка виділення та кнопок ---
    let selectedItem = null;
    let selectedFolderId = null; // Файл поза межами папки існувати не може

    const btnNewFile = document.getElementById('btn-new-file');
    const btnNewFolder = document.getElementById('btn-new-folder');
    const btnDelete = document.getElementById('btn-delete-item');
    const treeContainer = document.getElementById('explorer-tree');

    function updateToolbarState() {
        if (!selectedItem) {
            btnNewFile.disabled = true;
            btnNewFolder.disabled = false; // Можна створити рутову папку
            btnDelete.disabled = true;
            selectedFolderId = null;
        } else {
            const type = selectedItem.dataset.type;
            btnDelete.disabled = false;

            if (type === 'folder') {
                btnNewFile.disabled = false;
                btnNewFolder.disabled = false;
                selectedFolderId = selectedItem.dataset.id;
            } else if (type === 'file') {
                btnNewFile.disabled = true; // Виділено файл, а не папку
                btnNewFolder.disabled = false; // Папку створюємо поруч, або за логікою твого беку
                selectedFolderId = selectedItem.dataset.parent;
            }
        }
    }

    // Делегування подій для кліків по дереву
    treeContainer.addEventListener('click', (e) => {
        const item = e.target.closest('.tree-item');
        if (item) {
            // Зняти попереднє виділення
            if (selectedItem) selectedItem.classList.remove('selected');
            selectedItem = item;
            selectedItem.classList.add('selected');
            updateToolbarState();
        } else {
            // Клік по порожній області
            if (selectedItem) selectedItem.classList.remove('selected');
            selectedItem = null;
            updateToolbarState();
        }
    });

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

        const saveRename = () => {
            const newName = input.value.trim();
            const newSpan = document.createElement('span');
            newSpan.innerText = newName || oldName;
            input.replaceWith(newSpan);

            // TODO: Тут зробити AJAX запит до FoldersController/Edit або FilesController/Edit
        };

        input.addEventListener('blur', saveRename);
        input.addEventListener('keypress', (e) => { if (e.key === 'Enter') input.blur(); });
    });

    // --- 4. Маштабування панелей (Resizers) ---
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
            if (isHorizontal) {
                const newHeight = reverse ? startSize + (startPos - e.clientY) : startSize + (e.clientY - startPos);
                panel.style.height = `${newHeight}px`;
            } else {
                const newWidth = reverse ? startSize + (startPos - e.clientX) : startSize + (e.clientX - startPos);
                panel.style.width = `${newWidth}px`;
            }
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

    // --- 5. Відкриття / закриття панелей ---
    const btnToggleRight = document.getElementById('btn-toggle-right-panel');
    const rightPanel = document.getElementById('versions-panel');
    const resizerRight = document.getElementById('resizer-right');
    const btnCloseRight = document.getElementById('btn-close-right-panel');

    const toggleRightPanel = () => {
        const isHidden = rightPanel.style.display === 'none';
        rightPanel.style.display = isHidden ? 'flex' : 'none';
        resizerRight.style.display = isHidden ? 'block' : 'none';
    };

    btnToggleRight.addEventListener('click', toggleRightPanel);
    btnCloseRight.addEventListener('click', toggleRightPanel);

    const btnToggleBottom = document.getElementById('btn-toggle-bottom');
    const bottomContent = document.getElementById('meta-panel-content');
    const bottomPanel = document.getElementById('meta-panel');

    btnToggleBottom.addEventListener('click', () => {
        const isHidden = bottomContent.style.display === 'none';
        bottomContent.style.display = isHidden ? 'flex' : 'none';
        bottomPanel.style.height = isHidden ? '150px' : '35px';
        btnToggleBottom.innerHTML = isHidden ? '<i class="bi bi-chevron-down"></i>' : '<i class="bi bi-chevron-up"></i>';
    });

    // --- 6. Прив'язка до C# Backend (Моки для AJAX) ---
    document.getElementById('btn-new-file').addEventListener('click', () => {
        if (!selectedFolderId) return alert('Оберіть папку для збереження файлу!');
        // Виклик до бекенду: POST /Files/Create { Name: "NewFile.cs", FolderId: selectedFolderId }
        // При створенні файлу автоматично створювати Fileversion #1
        alert('Запит на створення файлу в папку ID: ' + selectedFolderId);
    });

    document.getElementById('btn-save-version').addEventListener('click', () => {
        const code = editor.getValue();
        // PUT /Fileversions/Edit/{currentVersionId} { Content: code }
        alert('Збереження в поточну версію...');
    });

    document.getElementById('btn-new-version').addEventListener('click', () => {
        const code = editor.getValue();
        // POST /Fileversions/Create { FileId: currentFileId, Content: code }
        alert('Створення нової версії на основі поточного коду...');
    });
});