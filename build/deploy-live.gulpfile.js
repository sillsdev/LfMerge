var gulp = require('gulp');
var download = require('gulp-download');
var fn = require('gulp-fn');
var jsonTransform = require('gulp-json-transform');
var rename = require("gulp-rename");
var rsync = require('gulp-rsync');
var savefile = require('gulp-savefile');

// TODO: Change "master" to "live" in URL below once live build is active on Jenkins
var baseUrl = "https://jenkins.lsdev.sil.org/view/LfMerge/view/All/job/LfMerge_Packaging-Linux-all-master-release/lastSuccessfulBuild/";

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
            .pipe(savefile()) // Shouldn't need to do this, but apparently gulp-rsync needs a locally-saved file
            .pipe(rsync({
                hostname: "svr01-vm106.saygoweb.com",
                username: "root",
                destination: "/root/lfmerge/",
                times: true,
                compress: true,
            }))
        }))
        // TODO: Find the "gulp-ssh" module and add a command to run "ssh vm106 dpkg -i lfmerge-live.deb".
})
