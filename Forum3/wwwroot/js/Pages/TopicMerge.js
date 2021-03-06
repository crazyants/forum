﻿$(function () {
	$("#load-more-topics").removeClass("hidden");
	$("#load-more-topics").on("click", LoadMoreTopics);
});

function LoadMoreTopics() {
	var originalText = $("#load-more-topics").text();

	$("#load-more-topics").text("Loading...");

	$.ajax({
		dataType: "html",
		url: "/topics/mergemore/" + window.id + "?page=" + (window.page + 1),
		success: function (data) {
			$("#topic-list").append(data);

			if (window.moreTopics)
				$("#load-more-topics").text(originalText);
			else
				$("#load-more-topics").hide();
		}
	});
}