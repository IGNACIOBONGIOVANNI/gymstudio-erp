FROM node:20-alpine

WORKDIR /app

# The command is kept generic because volume binds will bring package.json
# We install dependencies via compose or a script, but typically for local dev:
COPY package*.json ./
RUN npm install

# Command to run for local development
CMD ["npm", "run", "dev", "--", "--host"]
