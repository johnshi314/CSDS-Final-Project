"""
Entry point: ``python -m Server.webgl``

From the repository root with ``.env`` configured. Set ``WEBGL_ROOT`` to the folder
that contains ``index.html`` (e.g. ``Builds/Netflower`` on your laptop).
"""
from Server.webgl.server import main

if __name__ == "__main__":
    main()
