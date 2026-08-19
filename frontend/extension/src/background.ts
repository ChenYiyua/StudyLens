import { normaliseSelection, pendingSelectionKey } from './handoff'

const contextMenuId = 'explain-selection-with-studylens'

registerContextMenu()

function registerContextMenu() {
  chrome.contextMenus.removeAll(() => {
    chrome.contextMenus.create({
      id: contextMenuId,
      title: 'Explain “%s” with StudyLens',
      contexts: ['selection'],
    }, () => {
      void chrome.runtime.lastError
    })
  })
}

chrome.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId !== contextMenuId || !info.selectionText) return
  const text = normaliseSelection(info.selectionText)
  if (text.length < 2) return

  void openSelectionReview({
    text,
    title: tab?.title?.trim() || 'Current page',
    capturedAt: Date.now(),
  })
})

async function openSelectionReview(selection: { text: string; title: string; capturedAt: number }) {
  await chrome.storage.session.set({ [pendingSelectionKey]: selection })
  try {
    await chrome.action.openPopup()
  } catch {
    await chrome.windows.create({
      url: chrome.runtime.getURL('index.html'),
      type: 'popup',
      width: 440,
      height: 680,
    })
  }
}
