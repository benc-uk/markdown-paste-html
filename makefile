.PHONY: build clean run icon
.DEFAULT_GOAL := build

icon:
	rsrc -ico res/icon.ico -o rsrc.syso

build: icon
	GOOS=windows GOARCH=amd64 go build -ldflags="-H windowsgui -s -w" -o bin/MarkdownPaste.exe .

clean:
	rm -f bin/MarkdownPaste.exe rsrc.syso

run: build
	./bin/MarkdownPaste.exe

lint:
	GOOS=windows GOARCH=amd64 go vet ./...
