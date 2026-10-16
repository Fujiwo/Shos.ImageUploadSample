window.addEventListener('DOMContentLoaded', main)

// The MediaStream from the camera while it is running; null otherwise.
let currentStream = null

// Wires up the event handlers once the page has loaded.
function main() {
    document.querySelector('#camera').addEventListener('click', startCamera)
    document.querySelector('#snap').addEventListener('click', snap)
    document.querySelector('#uploadPicture').addEventListener('click', upload)

    // Bootstrap raises these events when the modal for the original image opens and closes.
    const originalModal = document.querySelector('#originalModal')
    originalModal.addEventListener('show.bs.modal', showOriginal)
    originalModal.addEventListener('hidden.bs.modal', clearOriginal)

    document.querySelectorAll('.delete-form').forEach(form => form.addEventListener('submit', confirmDelete))
}

// Cancels the delete form submission unless the user confirms it.
function confirmDelete(e) {
    if (!confirm(`Delete this image?\n${e.currentTarget.dataset.caption}`))
        e.preventDefault()
}

// Shows the original image of the clicked thumbnail in the modal.
function showOriginal(e) {
    // The thumbnail link that opened the modal. Its data-* attributes and href describe the image.
    const link          = e.relatedTarget
    const originalImage = document.querySelector('#originalImage')

    // Sets the size first to keep the aspect ratio while loading.
    originalImage.width  = link.dataset.width
    originalImage.height = link.dataset.height
    originalImage.alt    = link.dataset.caption
    originalImage.src    = link.href
    // textContent, not innerHTML: the description is user input.
    document.querySelector('#originalModalLabel').textContent = link.dataset.caption
}

// Prevents the previous image from flashing when the modal is opened next time.
function clearOriginal() {
    document.querySelector('#originalImage').removeAttribute('src')
}

// Stops all the tracks so that the camera is released (and its indicator light turns off).
function stopCamera() {
    if (currentStream) {
        currentStream.getTracks().forEach(track => track.stop())
        currentStream = null
    }
}

// Starts the camera and shows its live preview. Pressing the button again after a snap retakes the picture.
function startCamera(e) {
    if (e.cancelable)
        e.preventDefault()

    // Releases the previous stream, if any, before asking for a new one.
    stopCamera()

    const video      = document.querySelector('#video')
    // Only a preference ("ideal"); the browser picks the closest resolution the camera supports.
    const resolution = { width: 1080, height: 720 }

    navigator.mediaDevices.getUserMedia({
        audio: false,
        video: {
            width : { ideal: resolution.width  },
            height: { ideal: resolution.height }
        }
    }).then(function (stream) {
        currentStream   = stream
        video.srcObject = stream
    }).catch(function (error) {
        console.error('mediaDevice.getUserMedia() error:', error)
        alert('Could not start the camera.')
    })

    // Switches the UI to the live preview while the browser asks for permission.
    document.querySelector('#canvasPreview'   ).hidden = true
    document.querySelector('#uploadPicture'   ).hidden = true
    document.querySelector('#descriptionGroup').hidden = true
    document.querySelector('#videoPreview'    ).hidden = false
    document.querySelector('#snap'            ).hidden = false
}

// Copies the current video frame to the canvas and stops the camera.
function snap(e) {
    if (e.cancelable)
        e.preventDefault()

    const canvas  = document.querySelector('#canvas')
    const context = canvas.getContext('2d')
    const video   = document.querySelector('#video')
    // Scales the frame to the canvas size (600x400), whatever the camera resolution is.
    context.drawImage(video, 0, 0, canvas.width, canvas.height)

    stopCamera()

    // Switches the UI to the snapshot, ready to upload.
    document.querySelector('#videoPreview'    ).hidden = true
    document.querySelector('#snap'            ).hidden = true
    document.querySelector('#canvasPreview'   ).hidden = false
    document.querySelector('#descriptionGroup').hidden = false
    document.querySelector('#uploadPicture'   ).hidden = false
}

// Sends the snapshot on the canvas to POST UploadCameraImage, then reloads the list.
async function upload(e) {
    if (e.cancelable)
        e.preventDefault()

    const uploadButton = document.querySelector('#uploadPicture')
    const canvas       = document.querySelector('#canvas')
    // A data URL ("data:image/png;base64,..."), which the server decodes with ImageProcessor.ParseDataUrl.
    const picture      = canvas.toDataURL('image/png')
    const description  = document.querySelector('#description').value
    // The hidden antiforgery field rendered by the forms above. The request sends it in a header instead of the body.
    const token        = document.querySelector('input[name="__RequestVerificationToken"]').value

    // Prevents double uploads while the request is in flight.
    uploadButton.disabled = true

    try {
        const response = await fetch('@Url.Action("UploadCameraImage")', {
            method: 'POST',
            headers: { RequestVerificationToken: token },
            body: new URLSearchParams({
                imageBase64: picture,
                description: description
            })
        })

        if (!response.ok) {
            const message = await response.text()
            alert('Upload failed: ' + (message || response.statusText || `HTTP ${response.status}`))
            return
        }

        // Reloads the first page, where the new image appears.
        location.href = '@Url.Action("Upload")'
    } catch (error) {
        alert('Upload failed: ' + (error instanceof Error ? error.message : String(error)))
    } finally {
        uploadButton.disabled = false
    }
}
