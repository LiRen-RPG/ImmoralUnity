"""
Wrapper around the Gemini image-generation API.
"""

import mimetypes
import os
from typing import Optional

from google import genai
from google.genai import types

MODEL = "gemini-3.1-flash-image-preview"


def get_client() -> genai.Client:
    """Create a Gemini client using the 'Gemini_API_Key' environment variable."""
    api_key = os.environ.get("Gemini_API_Key") or ""
    if not api_key:
        raise EnvironmentError(
            "Environment variable 'Gemini_API_Key' is not set. "
            "Please set it before running this script."
        )
    return genai.Client(api_key=api_key)
def generate_image(
    client: genai.Client,
    prompt: str,
    reference_image_bytes: Optional[bytes] = None,
    aspect_ratio: str = "1:1",
    image_size: str = "1K",
) -> tuple[bytes, str]:
    """Call the Gemini model and return (image_bytes, file_extension).

    Args:
        client: Authenticated genai.Client.
        prompt: Text prompt describing the image to generate.
        reference_image_bytes: Optional PNG/JPEG bytes sent before the prompt
                               as a visual reference.
        aspect_ratio: Aspect ratio string accepted by ImageConfig.
        image_size: Size string accepted by ImageConfig ("1K", "2K", "4K").

    Returns:
        (img_bytes, ext) where ext is a file extension like ".png".

    Raises:
        RuntimeError: If the model returns no image data.
    """
    parts = []
    if reference_image_bytes:
        parts.append(types.Part.from_bytes(data=reference_image_bytes, mime_type="image/png"))
    parts.append(types.Part.from_text(text=prompt))

    contents = [types.Content(role="user", parts=parts)]

    config = types.GenerateContentConfig(
        thinking_config=types.ThinkingConfig(thinking_level="MINIMAL"),
        image_config=types.ImageConfig(
            aspect_ratio=aspect_ratio,
            image_size=image_size,
        ),
        response_modalities=["IMAGE"],
    )

    img_bytes = None
    img_ext = ".png"
    for chunk in client.models.generate_content_stream(
        model=MODEL,
        contents=contents,
        config=config,
    ):
        if chunk.parts is None:
            continue
        part = chunk.parts[0]
        if part.inline_data and part.inline_data.data:
            img_bytes = part.inline_data.data
            ext = mimetypes.guess_extension(part.inline_data.mime_type)
            if ext:
                img_ext = ext
            break

    if not img_bytes:
        raise RuntimeError("No image data returned by the model.")

    return img_bytes, img_ext
