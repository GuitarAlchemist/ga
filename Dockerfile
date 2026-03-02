FROM jupyter/base-notebook:2024.10.20

# Copy notebooks
COPY ./notebooks/ ${HOME}/notebooks/

# Fix permissions and create a non-root user (already exists in base image as jovyan)
RUN chown -R ${NB_UID}:${NB_GID} ${HOME}

# Keep jovyan user (already non-root in base image)
USER ${USER}

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8888/api || exit 1
