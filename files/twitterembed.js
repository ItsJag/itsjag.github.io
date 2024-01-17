// run a loop for all the infinite scrolling
setInterval(function timer() {
    const possibleTweets = document.querySelectorAll('article[data-testid="tweet"], div[aria-labelledby="modal-header"]')
  
    possibleTweets.forEach(function loop(tweet) {
      const shareLink = tweet.querySelector('[aria-label="Share post"]')
      if (!shareLink || typeof shareLink.dataset.vxLinked !== 'undefined') {
        return
      }
      // get the tweet link
      const link = [...tweet.querySelectorAll('a[href*="status"]')].map(function mapLinks(a) {
        return getVxTweetLink(a.href)
      }).filter(function getNumberLink (link) {
        return !!link
      })[0]
  
      // setup share logic
      shareLink.setAttribute('data-vx-linked', '')
      shareLink.addEventListener('click', createHandleShareClick(link), true)
      // console.debug("event listener added to " + link)
    })
  }, 1000)
  
  function createHandleShareClick (link) {

    function handleClick(event) {
      // update the clipboard after Twitter's copy fires
      setTimeout(async function hookToClipboard() {
        writeToClipboard(link)
      }, 50)
    }
  
    return function handleShareClick(event) {
      // handle after the menu triggers
      setTimeout(function hookToMenu() {
        // const menuItem = document.querySelector('#layers div:nth-of-type(3)[role="menuitem"]')
        const menuItem = document.querySelector('.r-1q9bdsx > div:nth-child(1) > div:nth-child(1) > div:nth-child(1) > div:nth-child(1)')
        menuItem.setAttribute('data-vx-linked', '')
        // menuItem.setAttribute('data-vx-linked', '')
        const menuText = document.querySelector('.r-1q9bdsx > div:nth-child(1) > div:nth-child(1) > div:nth-child(1) > div:nth-child(1) > div:nth-child(2) > div:nth-child(1) > span:nth-child(1)')
        menuText.textContent = 'Copy Embed'
        menuItem.addEventListener('click', handleClick, false)
      }, 50)
    }
  }

  
  // shared clipboard function
  async function writeToClipboard (text) {
    try {
      await navigator.clipboard.writeText(text)
    } catch (e) {
      // ignore error
    }
  }
  
  // shared function to get valid link from string
  function getVxTweetLink (text) {
    const link = text.match(/^http.*\/\d+/)[0]
    if (!link) {
      return link
    }
    const splitLink = link.split('//')
    return `${splitLink[0]}//vx${splitLink[1]}`
  }
  
  // keyboard shortcut for the current page :o
  document.addEventListener('keydown', handleKeyDown)
  
  function handleKeyDown(event) {
    const isCtrlOrCmd = event.ctrlKey || event.metaKey
    const isC = event.key.toLowerCase() === 'c'
    const isSelecting = window.getSelection().toString() !== ''
    if (!isSelecting && isCtrlOrCmd && isC) {
      // write current url, if valid, to clipboard
      const link = getVxTweetLink(window.location.href)
      writeToClipboard(link)
    }
  }
  

