"""
Tests that the HTTP endpoints report client errors as 400, not 500.

Every endpoint raises HTTPException(400) from inside a try block whose
`except Exception` clause turns anything it catches into a 500. HTTPException
is an Exception, so without an `except HTTPException: raise` clause ahead of
the generic one the deliberate 400 is swallowed and the client sees
500 {"detail": "400: <message>"}.
"""
import pytest
from fastapi.testclient import TestClient

from main import app, get_graphiti_service


class ErroringService:
    """Stand-in service whose operations all report a domain-level error."""

    MESSAGE = "episode has no user"

    async def add_episode(self, episode):
        return {"status": "error", "message": self.MESSAGE}

    async def search_knowledge(self, search):
        return {"status": "error", "message": self.MESSAGE}

    async def get_recommendations(self, recommendation):
        return {"status": "error", "message": self.MESSAGE}

    async def get_user_progress(self, user_id):
        return {"status": "error", "message": self.MESSAGE}


@pytest.fixture
def client():
    # Not used as a context manager on purpose: the lifespan builds a real
    # GraphitiMusicService against FalkorDB, which a unit test has no business
    # starting. The dependency override is what the endpoints resolve.
    app.dependency_overrides[get_graphiti_service] = ErroringService
    yield TestClient(app, raise_server_exceptions=False)
    app.dependency_overrides.clear()


@pytest.mark.parametrize(
    "method,url,payload",
    [
        ("post", "/episodes", {"user_id": "u1", "episode_type": "practice", "content": {}}),
        ("post", "/search", {"query": "jazz chords", "user_id": "u1"}),
        ("post", "/recommendations", {"user_id": "u1", "recommendation_type": "next_chord", "context": {}}),
        ("get", "/users/u1/progress", None),
    ],
)
def test_domain_error_is_reported_as_400(client, method, url, payload):
    response = client.request(method, url, json=payload)

    assert response.status_code == 400, response.text
    assert response.json()["detail"] == ErroringService.MESSAGE
