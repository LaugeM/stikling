// The @ picker in a note box needs to know where the caret is, and to put it back after a name is inserted.

export function caret(box) {
    return box ? box.selectionStart ?? 0 : 0;
}

export function setCaret(box, position) {
    if (!box)
        return;
    box.focus();
    box.setSelectionRange(position, position);
}
