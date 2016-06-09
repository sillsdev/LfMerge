var gulp = require('gulp');
var download = require('gulp-download');
var fn = require('gulp-fn');
var jsonTransform = require('gulp-json-transform');
var rename = require("gulp-rename");
var savefile = require('gulp-savefile');
var shell = require('gulp-shell');

// TODO: Change "lastCompletedBuild" to "lastSuccessfulBuild" in URL below
// once the lastSuccessfulBuild target is being correctly published in Jenkins
var baseUrl = "https://jenkins.lsdev.sil.org/view/LfMerge/view/All/job/LfMerge_Packaging-Linux-all-live-release/lastCompletedBuild/";

gulp.task('default', function() {
    download(baseUrl + "/api/json")
        .pipe(jsonTransform(function(build) {
            return baseUrl + "artifact/" + build.artifacts.find(function(item) {
                    return item.fileName.endsWith('.deb');
                }).relativePath;
        }))
        .pipe(fn(function(file) { // Gulp has wrapped our string in a "file" object we need to unpack
            var url = file.contents.toString();
            console.log("URL is:", url)
            download(url)
            .pipe(rename({
                // Taking the version number OUT of the filename allows us to have only one package file ever on the
                // live server, instead of an ever-increasing number of .deb files. However, we might decide that
                // we actually WANT an ever-increasing number of .deb files on the server, with version numbers in
                // the filenames. If that's the case, just remove this rename() operation from the Gulp pipeline.
                basename: "lfmerge-live"
            }))
            .pipe(savefile())
            .pipe(shell([
                'echo About to start rsync',
                'rsync -vtRz --progress lfmerge-live.deb root@svr01-vm106.saygoweb.com:/root/lfmerge/',
                'echo Done with rsync',
                'ssh root@svr01-vm106.saygoweb.com echo Hi',  // This will eventually be "ssh vm106 dpkg -i lfmerge-live.deb"
                'echo Finished download task',
            ]))
        }))
})
