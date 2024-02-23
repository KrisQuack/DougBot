# Use an official Python runtime as a parent image
FROM python:3.12-slim

# Set the working directory in the container
WORKDIR /app

# Copy the current directory contents into the container at /app
COPY ./Dougbot.Twitch/ /app

# Install build essentials, compile your dependencies, then remove build essentials
RUN apt-get update && \
    apt-get install -y --no-install-recommends gcc build-essential && \
    pip install --no-cache-dir --trusted-host pypi.python.org -r requirements.txt && \
    apt-get purge -y --auto-remove gcc build-essential && \
    rm -rf /var/lib/apt/lists/*

# Run Main.py when the container launches
CMD ["python", "Dougbot.Twitch.py"]