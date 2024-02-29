# Use an official Python runtime as a parent image
FROM python:3.12-slim

# Set the working directory in the container
WORKDIR /app

# Copy the current directory contents into the container at /app
COPY ./Dougbot.Twitch/ /app

# Install build essentials, compile your dependencies, then remove build essentials
RUN pip install --no-cache-dir --trusted-host pypi.python.org -r requirements.txt

# Run Main.py when the container launches
CMD ["python", "Dougbot.Twitch.py"]